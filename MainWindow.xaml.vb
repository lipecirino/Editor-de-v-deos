Imports FFMpegCore
Imports Microsoft.Win32
Imports System.Windows
Imports System.Windows.Threading
Imports System.IO
Imports System.Threading
Imports System.Windows.Controls.Primitives
Imports System.Windows.Input
Imports System.Windows.Media.Animation
Imports System.Windows.Media
Imports System.ComponentModel
Imports System.Collections.ObjectModel
Imports System.Collections.Generic
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Diagnostics
Imports System.Windows.Media.Imaging

' --- CLASSE DE DADOS PARA CACHE DE CRONOANÁLISE ---
<Serializable()>
Public Class CronoAnaliseCacheEntry
    Public Property Operacao As String = ""
    Public Property Cliente As String = ""
    Public Property NumeroAmostras As Integer = 0
    Public Property Inicio1 As Double = 0
    Public Property Fim1 As Double = 0
    Public Property Inicio2 As Double = 0
    Public Property Fim2 As Double = 0
    Public Property Inicio3 As Double = 0
    Public Property Fim3 As Double = 0
    Public Property Inicio4 As Double = 0
    Public Property Fim4 As Double = 0
End Class

<Serializable()>
Public Class VideoCacheData
    Public Property CaminhoVideo As String = ""
    Public Property NomeVideo As String = ""
    Public Property TemCronoAnalise As Boolean = False
    Public Property CronoAnalises As New List(Of CronoAnaliseCacheEntry)()
End Class

<Serializable()>
Public Class CacheGlobal
    Public Property Videos As New List(Of VideoCacheData)()
End Class

' --- SEGMENTO DE CORTE (INÍCIO/FIM EM SEGUNDOS) ---
Public Class SegmentoCorte
    Public Property Inicio As Double = 0
    Public Property Fim As Double = 0
    Public Property Rotulo As String = ""   ' nome da operação (usado na visão geral da cronoanálise)
    Public Property Dono As CronoAnaliseEntry = Nothing   ' linha da cronoanálise de origem (visão geral)

    Public ReadOnly Property Duracao As Double
        Get
            Return Math.Max(0, Fim - Inicio)
        End Get
    End Property
End Class

' --- CLASSE DE DADOS INDIVIDUAIS ---
Public Class VideoTarefa
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private _caminho As String
    Private _temCronoAnalise As Boolean = False

    Public Property Caminho As String
        Get
            Return _caminho
        End Get
        Set(value As String)
            _caminho = value
        End Set
    End Property

    ' Primeiro tempo registrado do próximo segmento (-1 = nenhum)
    Public Property TempoPendente As Double = -1
    Public Property FiltroFFmpeg As String = ""

    ' Cada segmento é exportado como um vídeo separado (sem segmentos, exporta o vídeo inteiro)
    Public Property Segmentos As New List(Of SegmentoCorte)()

    Public Property TemCronoAnalise As Boolean
        Get
            Return _temCronoAnalise
        End Get
        Set(value As Boolean)
            If value <> _temCronoAnalise Then
                _temCronoAnalise = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(TemCronoAnalise)))
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(NomeComFlag)))
            End If
        End Set
    End Property

    ' Tem cortes definidos (ou 1º tempo pendente) a serem feitos: usado no indicador da galeria
    Private _temCortes As Boolean = False

    Public Property TemCortes As Boolean
        Get
            Return _temCortes
        End Get
        Set(value As Boolean)
            If value <> _temCortes Then
                _temCortes = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(TemCortes)))
            End If
        End Set
    End Property

    ' --- EXPORTAÇÃO: o card do vídeo na galeria vira uma barra de progresso ---
    Private _exportando As Boolean = False
    Private _progresso As Double = 0   ' 0..1

    Public Property Exportando As Boolean
        Get
            Return _exportando
        End Get
        Set(value As Boolean)
            If value <> _exportando Then
                _exportando = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Exportando)))
            End If
        End Set
    End Property

    Public Property Progresso As Double
        Get
            Return _progresso
        End Get
        Set(value As Double)
            value = Math.Max(0, Math.Min(1, value))
            If value <> _progresso Then
                _progresso = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Progresso)))
            End If
        End Set
    End Property

    Public ReadOnly Property NomeComFlag As String
        Get
            Return Path.GetFileName(Caminho)
        End Get
    End Property

    ' Rotação só da visualização deste vídeo, em graus acumulados (múltiplos de 90; não afeta a exportação)
    Public Property RotacaoVisualizacao As Double = 0

    ' --- Miniatura e duração exibidas no card da galeria (carregadas em segundo plano) ---
    Private _miniatura As ImageSource
    Private _duracaoTexto As String = ""

    Public Property Miniatura As ImageSource
        Get
            Return _miniatura
        End Get
        Set(value As ImageSource)
            _miniatura = value
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Miniatura)))
        End Set
    End Property

    Public Property DuracaoTexto As String
        Get
            Return _duracaoTexto
        End Get
        Set(value As String)
            _duracaoTexto = value
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(DuracaoTexto)))
        End Set
    End Property

    Public Overrides Function ToString() As String
        Return NomeComFlag
    End Function
End Class

' --- JANELA FLUTUANTE PARA CRONOANÁLISE ---
Public Class CronoPanelWindow
    Inherits Window

    Private _mainWindow As MainWindow
    Private _cronoPanel As Border

    Public Sub New(mainWindow As MainWindow, cronoPanel As Border)
        Me._mainWindow = mainWindow
        Me._cronoPanel = cronoPanel

        ' Configurar a janela flutuante
        Me.Title = "Cronoanálise"
        Me.Width = 1250
        Me.Height = 350
        Me.WindowStartupLocation = WindowStartupLocation.CenterOwner
        Me.Owner = mainWindow

        ' Criar o container para o painel
        Dim grid = New Grid()
        grid.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(1, GridUnitType.Star)})

        ' Mover o painel para dentro da janela
        Dim parent = VisualTreeHelper.GetParent(_cronoPanel)
        If TypeOf parent Is Panel Then
            CType(parent, Panel).Children.Remove(_cronoPanel)
        End If

        ' Adicionar o painel à janela
        Grid.SetRow(_cronoPanel, 0)
        grid.Children.Add(_cronoPanel)
        Me.Content = grid

        ' Quando a janela fechar, re-acoplar o painel
        AddHandler Me.Closed, Sub()
                                  _mainWindow.ReacoplarCronoPanel(_cronoPanel)
                              End Sub
    End Sub

    Public Property CronoPanel As Border
        Get
            Return _cronoPanel
        End Get
        Set(value As Border)
            _cronoPanel = value
        End Set
    End Property

    Public ReadOnly Property MainWindow As MainWindow
        Get
            Return _mainWindow
        End Get
    End Property

End Class

' --- CLASSE PARA O MODO CRONOANÁLISE ---
Public Class CronoAnaliseEntry
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Private _operacao As String

    ' 8 campos: 4 pares (início/fim)
    Private _valores(7) As Double
    Private _displays(7) As String
    Private _proximoCampo As Integer = 0

    Private Sub RaisePropertyChanged(propName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propName))
    End Sub

    Public Property Operacao As String
        Get
            Return _operacao
        End Get
        Set(value As String)
            _operacao = value
            RaisePropertyChanged(NameOf(Operacao))
        End Set
    End Property

    Private _numeroAmostras As Integer = 0

    Public Property NumeroAmostras As Integer
        Get
            Return _numeroAmostras
        End Get
        Set(value As Integer)
            If value < 0 Then value = 0
            _numeroAmostras = value
            RaisePropertyChanged(NameOf(NumeroAmostras))
        End Set
    End Property

    Private _cliente As String

    Public Property Cliente As String
        Get
            Return _cliente
        End Get
        Set(value As String)
            _cliente = value
            RaisePropertyChanged(NameOf(Cliente))
        End Set
    End Property

    ' False quando há cortes reais a fazer no vídeo (a visualização fica desabilitada)
    Private _podeVisualizar As Boolean = True

    Public Property PodeVisualizar As Boolean
        Get
            Return _podeVisualizar
        End Get
        Set(value As Boolean)
            If _podeVisualizar = value Then Return
            _podeVisualizar = value
            RaisePropertyChanged(NameOf(PodeVisualizar))
        End Set
    End Property

    ' Indica que a reprodução está travada nos trechos desta linha (botão "visualizar")
    Private _visualizando As Boolean = False

    Public Property Visualizando As Boolean
        Get
            Return _visualizando
        End Get
        Set(value As Boolean)
            If _visualizando = value Then Return
            _visualizando = value
            RaisePropertyChanged(NameOf(Visualizando))
        End Set
    End Property

    ' Índices dos campos
    Public Const IDX_INICIO1 As Integer = 0
    Public Const IDX_FIM1 As Integer = 1
    Public Const IDX_INICIO2 As Integer = 2
    Public Const IDX_FIM2 As Integer = 3
    Public Const IDX_INICIO3 As Integer = 4
    Public Const IDX_FIM3 As Integer = 5
    Public Const IDX_INICIO4 As Integer = 6
    Public Const IDX_FIM4 As Integer = 7

    ' Propriedades de valor (segundos)
    Public Property Inicio1 As Double
        Get
            Return _valores(IDX_INICIO1)
        End Get
        Set(value As Double)
            _valores(IDX_INICIO1) = value
            Inicio1Display = SegundosParaCrono(value)
            RaisePropertyChanged(NameOf(Inicio1))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End Set
    End Property

    Public Property Fim1 As Double
        Get
            Return _valores(IDX_FIM1)
        End Get
        Set(value As Double)
            _valores(IDX_FIM1) = value
            Fim1Display = SegundosParaCrono(value)
            RaisePropertyChanged(NameOf(Fim1))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End Set
    End Property

    Public Property Inicio2 As Double
        Get
            Return _valores(IDX_INICIO2)
        End Get
        Set(value As Double)
            _valores(IDX_INICIO2) = value
            Inicio2Display = SegundosParaCrono(value)
            RaisePropertyChanged(NameOf(Inicio2))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End Set
    End Property

    Public Property Fim2 As Double
        Get
            Return _valores(IDX_FIM2)
        End Get
        Set(value As Double)
            _valores(IDX_FIM2) = value
            Fim2Display = SegundosParaCrono(value)
            RaisePropertyChanged(NameOf(Fim2))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End Set
    End Property

    Public Property Inicio3 As Double
        Get
            Return _valores(IDX_INICIO3)
        End Get
        Set(value As Double)
            _valores(IDX_INICIO3) = value
            Inicio3Display = SegundosParaCrono(value)
            RaisePropertyChanged(NameOf(Inicio3))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End Set
    End Property

    Public Property Fim3 As Double
        Get
            Return _valores(IDX_FIM3)
        End Get
        Set(value As Double)
            _valores(IDX_FIM3) = value
            Fim3Display = SegundosParaCrono(value)
            RaisePropertyChanged(NameOf(Fim3))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End Set
    End Property

    Public Property Inicio4 As Double
        Get
            Return _valores(IDX_INICIO4)
        End Get
        Set(value As Double)
            _valores(IDX_INICIO4) = value
            Inicio4Display = SegundosParaCrono(value)
            RaisePropertyChanged(NameOf(Inicio4))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End Set
    End Property

    Public Property Fim4 As Double
        Get
            Return _valores(IDX_FIM4)
        End Get
        Set(value As Double)
            _valores(IDX_FIM4) = value
            Fim4Display = SegundosParaCrono(value)
            RaisePropertyChanged(NameOf(Fim4))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End Set
    End Property

    ' Propriedades de display (mm:ss,cc)
    Public Property Inicio1Display As String
        Get
            Return _displays(IDX_INICIO1)
        End Get
        Set(value As String)
            If value <> _displays(IDX_INICIO1) Then
                _displays(IDX_INICIO1) = value
                Dim parsed = CronoParaSegundos(value)
                If parsed.HasValue Then
                    _valores(IDX_INICIO1) = parsed.Value
                    RaisePropertyChanged(NameOf(Inicio1))
                    RaisePropertyChanged(NameOf(DuracaoDisplay))
                End If
                RaisePropertyChanged(NameOf(Inicio1Display))
            End If
        End Set
    End Property

    Public Property Fim1Display As String
        Get
            Return _displays(IDX_FIM1)
        End Get
        Set(value As String)
            If value <> _displays(IDX_FIM1) Then
                _displays(IDX_FIM1) = value
                Dim parsed = CronoParaSegundos(value)
                If parsed.HasValue Then
                    _valores(IDX_FIM1) = parsed.Value
                    RaisePropertyChanged(NameOf(Fim1))
                    RaisePropertyChanged(NameOf(DuracaoDisplay))
                End If
                RaisePropertyChanged(NameOf(Fim1Display))
            End If
        End Set
    End Property

    Public Property Inicio2Display As String
        Get
            Return _displays(IDX_INICIO2)
        End Get
        Set(value As String)
            If value <> _displays(IDX_INICIO2) Then
                _displays(IDX_INICIO2) = value
                Dim parsed = CronoParaSegundos(value)
                If parsed.HasValue Then
                    _valores(IDX_INICIO2) = parsed.Value
                    RaisePropertyChanged(NameOf(Inicio2))
                    RaisePropertyChanged(NameOf(DuracaoDisplay))
                End If
                RaisePropertyChanged(NameOf(Inicio2Display))
            End If
        End Set
    End Property

    Public Property Fim2Display As String
        Get
            Return _displays(IDX_FIM2)
        End Get
        Set(value As String)
            If value <> _displays(IDX_FIM2) Then
                _displays(IDX_FIM2) = value
                Dim parsed = CronoParaSegundos(value)
                If parsed.HasValue Then
                    _valores(IDX_FIM2) = parsed.Value
                    RaisePropertyChanged(NameOf(Fim2))
                    RaisePropertyChanged(NameOf(DuracaoDisplay))
                End If
                RaisePropertyChanged(NameOf(Fim2Display))
            End If
        End Set
    End Property

    Public Property Inicio3Display As String
        Get
            Return _displays(IDX_INICIO3)
        End Get
        Set(value As String)
            If value <> _displays(IDX_INICIO3) Then
                _displays(IDX_INICIO3) = value
                Dim parsed = CronoParaSegundos(value)
                If parsed.HasValue Then
                    _valores(IDX_INICIO3) = parsed.Value
                    RaisePropertyChanged(NameOf(Inicio3))
                    RaisePropertyChanged(NameOf(DuracaoDisplay))
                End If
                RaisePropertyChanged(NameOf(Inicio3Display))
            End If
        End Set
    End Property

    Public Property Fim3Display As String
        Get
            Return _displays(IDX_FIM3)
        End Get
        Set(value As String)
            If value <> _displays(IDX_FIM3) Then
                _displays(IDX_FIM3) = value
                Dim parsed = CronoParaSegundos(value)
                If parsed.HasValue Then
                    _valores(IDX_FIM3) = parsed.Value
                    RaisePropertyChanged(NameOf(Fim3))
                    RaisePropertyChanged(NameOf(DuracaoDisplay))
                End If
                RaisePropertyChanged(NameOf(Fim3Display))
            End If
        End Set
    End Property

    Public Property Inicio4Display As String
        Get
            Return _displays(IDX_INICIO4)
        End Get
        Set(value As String)
            If value <> _displays(IDX_INICIO4) Then
                _displays(IDX_INICIO4) = value
                Dim parsed = CronoParaSegundos(value)
                If parsed.HasValue Then
                    _valores(IDX_INICIO4) = parsed.Value
                    RaisePropertyChanged(NameOf(Inicio4))
                    RaisePropertyChanged(NameOf(DuracaoDisplay))
                End If
                RaisePropertyChanged(NameOf(Inicio4Display))
            End If
        End Set
    End Property

    Public Property Fim4Display As String
        Get
            Return _displays(IDX_FIM4)
        End Get
        Set(value As String)
            If value <> _displays(IDX_FIM4) Then
                _displays(IDX_FIM4) = value
                Dim parsed = CronoParaSegundos(value)
                If parsed.HasValue Then
                    _valores(IDX_FIM4) = parsed.Value
                    RaisePropertyChanged(NameOf(Fim4))
                    RaisePropertyChanged(NameOf(DuracaoDisplay))
                End If
                RaisePropertyChanged(NameOf(Fim4Display))
            End If
        End Set
    End Property

    ' Retorna o índice do próximo campo vazio (0-7) ou -1 se todos cheios
    Public Function ObterProximoCampoVazio() As Integer
        For i As Integer = 0 To 7
            If _valores(i) = 0 Then
                Return i
            End If
        Next
        Return -1
    End Function

    ' Define o valor no índice especificado
    Public Sub SetValorCampo(indice As Integer, valor As Double)
        If indice < 0 OrElse indice > 7 Then Return
        Select Case indice
            Case IDX_INICIO1 : Inicio1 = valor
            Case IDX_FIM1 : Fim1 = valor
            Case IDX_INICIO2 : Inicio2 = valor
            Case IDX_FIM2 : Fim2 = valor
            Case IDX_INICIO3 : Inicio3 = valor
            Case IDX_FIM3 : Fim3 = valor
            Case IDX_INICIO4 : Inicio4 = valor
            Case IDX_FIM4 : Fim4 = valor
        End Select
    End Sub

    Public Shared Function SegundosParaCrono(segundos As Double) As String
        If segundos = 0 Then Return ""
        Dim ts = TimeSpan.FromSeconds(segundos)
        Dim centesimos = CInt((ts.Milliseconds / 10))
        Dim totalMin = Int(ts.TotalMinutes)
        Return $"{totalMin:00}:{ts.Seconds:00},{centesimos:00}"
    End Function

    Public Shared Function CronoParaSegundos(crono As String) As Double?
        If String.IsNullOrWhiteSpace(crono) Then Return Nothing
        crono = crono.Trim().Replace(",", ".")
        Dim match = System.Text.RegularExpressions.Regex.Match(crono, "^(?:(\d+):)?(\d+)(?:\.(\d+))?$")
        If match.Success Then
            Dim minutos As Double = 0
            Dim segundos As Double = 0
            Dim centesimos As Double = 0
            If match.Groups(1).Success Then Double.TryParse(match.Groups(1).Value, minutos)
            If match.Groups(2).Success Then Double.TryParse(match.Groups(2).Value, segundos)
            If match.Groups(3).Success Then
                Dim cents As String = match.Groups(3).Value.PadRight(2, "0"c).Substring(0, 2)
                Double.TryParse(cents, centesimos)
            End If
            Return minutos * 60 + segundos + centesimos / 100.0
        End If
        Return Nothing
    End Function

    ' Duração total calculada: soma dos pares (Fim - Início)
    Public ReadOnly Property DuracaoDisplay As String
        Get
            Dim total As Double = 0
            total += Math.Max(0, Fim1 - Inicio1)
            total += Math.Max(0, Fim2 - Inicio2)
            total += Math.Max(0, Fim3 - Inicio3)
            total += Math.Max(0, Fim4 - Inicio4)
            If total <= 0 Then Return ""
            ' Formatar como segundos,centésimos (ex: 125,67)
            Dim segundosInt = CInt(Math.Floor(total))
            Dim centesimos = CInt((total - segundosInt) * 100)
            Return $"{segundosInt},{centesimos:D2}"
        End Get
    End Property

    ' Duração em segundos (para cálculos)
    Public ReadOnly Property DuracaoSegundos As Double
        Get
            Dim total As Double = 0
            total += Math.Max(0, Fim1 - Inicio1)
            total += Math.Max(0, Fim2 - Inicio2)
            total += Math.Max(0, Fim3 - Inicio3)
            total += Math.Max(0, Fim4 - Inicio4)
            Return total
        End Get
    End Property

    ' Propriedades para análise estatística (serão calculadas externamente)
    Private _desvioPercentual As Double = 0
    Private _barraLargura As Double = 0
    Private _barraCorBrush As Brush = Brushes.Gray
    Private _desvioTexto As String = ""

    Public Property DesvioPercentual As Double
        Get
            Return _desvioPercentual
        End Get
        Set(value As Double)
            _desvioPercentual = value
            RaisePropertyChanged(NameOf(DesvioPercentual))
            RaisePropertyChanged(NameOf(DesvioTexto))
            RaisePropertyChanged(NameOf(BarraLarguraEsq))
            RaisePropertyChanged(NameOf(BarraLarguraDir))
        End Set
    End Property

    Public Property BarraLargura As Double
        Get
            Return _barraLargura
        End Get
        Set(value As Double)
            _barraLargura = value
            RaisePropertyChanged(NameOf(BarraLargura))
            RaisePropertyChanged(NameOf(BarraLarguraEsq))
            RaisePropertyChanged(NameOf(BarraLarguraDir))
        End Set
    End Property

    ' Largura da barra para cada lado do eixo central (a trilha tem largura fixa)
    Public ReadOnly Property BarraLarguraEsq As Double
        Get
            Return If(_desvioPercentual < 0, _barraLargura, 0)
        End Get
    End Property

    Public ReadOnly Property BarraLarguraDir As Double
        Get
            Return If(_desvioPercentual >= 0, _barraLargura, 0)
        End Get
    End Property

    Public Property BarraCorBrush As Brush
        Get
            Return _barraCorBrush
        End Get
        Set(value As Brush)
            _barraCorBrush = value
            RaisePropertyChanged(NameOf(BarraCorBrush))
        End Set
    End Property

    Public ReadOnly Property DesvioTexto As String
        Get
            If DuracaoSegundos = 0 Then Return ""
            Dim sinal = If(_desvioPercentual >= 0, "+", "")
            Return $"{sinal}{_desvioPercentual:F1}%"
        End Get
    End Property
End Class

Partial Class MainWindow
    Inherits Window

    Private cronometroVideo As New DispatcherTimer()
    Private videoAtual As VideoTarefa = Nothing
    Private mediaTimeline As MediaTimeline
    Private mediaClock As MediaClock
    Private _velocidadeReproducao As Double = 1.0

    ' --- REPRODUÇÃO FLUIDA COM FRAMES EM MEMÓRIA ---
    Private estaReproduzindo As Boolean = False
    Private isDraggingSlider As Boolean = False
    Private frameRate As Double = 30.0

    ' Variáveis de controlo de processamento
    Private estaProcessando As Boolean = False
    Private cts As CancellationTokenSource

    ' --- BUFFER DE FRAMES PARA NAVEGAÇÃO RÁPIDA ---
    Private frameBuffer As New Dictionary(Of Integer, BitmapImage)()
    Private frameBufferLock As New Object()
    Private bufferTaskCts As CancellationTokenSource
    Private ultimaPosicaoBuffer As Double = -1
    Private segmentoTravado As SegmentoCorte = Nothing
    Private Const BUFFER_SEGUNDOS As Double = 3.0 ' 3 segundos para frente e para trás

    ' --- MODO CRONOANÁLISE ---
    Private modoCronoAtivo As Boolean = False
    Private cronoEntries As New ObservableCollection(Of CronoAnaliseEntry)()
    Private indiceCronoSelecionado As Integer = -1
    Private editandoCelulaCrono As Boolean = False

    ' --- PRÉ-VISUALIZAÇÃO DOS TRECHOS DE UMA LINHA DA CRONOANÁLISE ---
    ' previewEntry: linha selecionada, cujos trechos aparecem na timeline como cortes (não altera os cortes reais)
    ' previewTrava: reprodução restrita a esses trechos (botão "visualizar" da linha)
    Private previewEntry As CronoAnaliseEntry = Nothing
    Private previewTrava As Boolean = False
    Private segmentosPreview As New List(Of SegmentoCorte)()
    Private ultimoSaltoPreviewTick As Integer = 0

    ' --- VISÃO GERAL: barras de tempo de TODAS as operações da cronoanálise ---
    Private verTodos As Boolean = False
    Private segmentosTodos As New List(Of SegmentoCorte)()

    Private Sub RecalcularSegmentosTodos()
        segmentosTodos = New List(Of SegmentoCorte)()
        For Each en In cronoEntries
            Dim ini = {en.Inicio1, en.Inicio2, en.Inicio3, en.Inicio4}
            Dim fim = {en.Fim1, en.Fim2, en.Fim3, en.Fim4}
            For i = 0 To 3
                If fim(i) > ini(i) AndAlso fim(i) > 0 Then
                    segmentosTodos.Add(New SegmentoCorte() With {.Inicio = ini(i), .Fim = fim(i), .Rotulo = If(en.Operacao, ""), .Dono = en})
                End If
            Next
        Next
        segmentosTodos.Sort(Function(x, y) x.Inicio.CompareTo(y.Inicio))
    End Sub

    Private Sub EntryTodos_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If Not verTodos OrElse e.PropertyName Is Nothing Then Return
        If Not (e.PropertyName.StartsWith("Inicio") OrElse e.PropertyName.StartsWith("Fim") OrElse e.PropertyName = "Operacao") Then Return
        If e.PropertyName.EndsWith("Display") Then Return
        RecalcularSegmentosTodos()
        segmentoTravado = Nothing
        AtualizarSegmentosVisuais()
    End Sub

    Private Sub DefinirVerTodos(ligado As Boolean)
        verTodos = ligado
        If btnVerTodasCrono.IsChecked <> ligado Then btnVerTodasCrono.IsChecked = ligado
        segmentoTravado = Nothing
        If ligado Then
            ' a visão geral substitui a visualização de uma única linha
            If previewTrava AndAlso previewEntry IsNot Nothing Then
                previewTrava = False
                previewEntry.Visualizando = False
                AplicarModoPreview()
            End If
            RecalcularSegmentosTodos()
        End If
        AtualizarSegmentosVisuais()
    End Sub

    Private Sub BtnVerTodasCrono_Click(sender As Object, e As RoutedEventArgs)
        DefinirVerTodos(btnVerTodasCrono.IsChecked = True)
    End Sub

    ' Cortes mostrados na timeline: visão geral da cronoanálise, trechos da linha selecionada ou, sem seleção, os cortes reais
    Private Function SegmentosExibidos() As List(Of SegmentoCorte)
        If verTodos Then Return segmentosTodos
        If previewEntry IsNot Nothing Then Return segmentosPreview
        Return If(videoAtual IsNot Nothing, videoAtual.Segmentos, New List(Of SegmentoCorte)())
    End Function

    ' Cronoanálise e cortes são excludentes no mesmo vídeo: com linhas no grid (ou modo crono ligado) não se corta;
    ' com cortes a fazer não se faz cronoanálise.
    Private Function TemCronoNoVideo() As Boolean
        Return videoAtual IsNot Nothing AndAlso (cronoEntries.Count > 0 OrElse modoCronoAtivo)
    End Function

    Private Sub AtualizarBloqueiosCronoCorte()
        Dim cortes As Boolean = TemCortesNoVideo()
        Dim crono As Boolean = TemCronoNoVideo()

        ' Botão Crono
        If cortes Then
            If btnModoCrono.IsChecked = True Then btnModoCrono.IsChecked = False
            btnModoCrono.IsEnabled = False
            btnModoCrono.ToolTip = "Indisponível: este vídeo tem cortes a fazer. A cronoanálise só pode ser feita em vídeo já cortado."
        Else
            btnModoCrono.IsEnabled = True
            btnModoCrono.ToolTip = "Ativar/Desativar modo cronoanálise"
        End If

        ' Botão de cortes
        btnRegistrarTempo.IsEnabled = Not crono AndAlso Not estaProcessando
        btnRegistrarTempo.ToolTip = If(crono,
            "Indisponível: este vídeo tem cronoanálise. Não é possível cortar um vídeo com cronoanálise.",
            "Clique no início e depois no fim do trecho. O menor tempo vira o início e o maior o fim do segmento.")
    End Sub

    ' A visualização da cronoanálise só vale quando não há cortes reais (ou 1º tempo pendente) no vídeo
    Private Function TemCortesNoVideo() As Boolean
        Return videoAtual IsNot Nothing AndAlso (videoAtual.Segmentos.Count > 0 OrElse videoAtual.TempoPendente >= 0)
    End Function

    Private Sub AtualizarPodeVisualizar()
        Dim permitido As Boolean = Not TemCortesNoVideo()
        For Each ce In cronoEntries
            ce.PodeVisualizar = permitido
        Next
    End Sub

    Private Sub RecalcularSegmentosPreview()
        segmentosPreview = New List(Of SegmentoCorte)()
        If previewEntry Is Nothing Then Return
        Dim ini = {previewEntry.Inicio1, previewEntry.Inicio2, previewEntry.Inicio3, previewEntry.Inicio4}
        Dim fim = {previewEntry.Fim1, previewEntry.Fim2, previewEntry.Fim3, previewEntry.Fim4}
        For i = 0 To 3
            If fim(i) > ini(i) AndAlso fim(i) > 0 Then
                segmentosPreview.Add(New SegmentoCorte() With {.Inicio = ini(i), .Fim = fim(i)})
            End If
        Next
        segmentosPreview.Sort(Function(x, y) x.Inicio.CompareTo(y.Inicio))
    End Sub

    Private Sub PreviewEntry_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName Is Nothing OrElse Not (e.PropertyName.StartsWith("Inicio") OrElse e.PropertyName.StartsWith("Fim")) Then Return
        If e.PropertyName.EndsWith("Display") Then Return
        RecalcularSegmentosPreview()
        segmentoTravado = Nothing
        AtualizarSegmentosVisuais()
    End Sub

    ' Mostra na timeline os trechos da linha (mantém o estado de trava ao trocar de linha)
    Private Sub MostrarPreviewCrono(entry As CronoAnaliseEntry)
        If Object.ReferenceEquals(entry, previewEntry) Then Return
        If previewEntry IsNot Nothing Then
            RemoveHandler previewEntry.PropertyChanged, AddressOf PreviewEntry_PropertyChanged
            previewEntry.Visualizando = False
        End If
        previewEntry = entry
        segmentoTravado = Nothing
        If entry IsNot Nothing Then
            AddHandler entry.PropertyChanged, AddressOf PreviewEntry_PropertyChanged
            entry.Visualizando = previewTrava
        Else
            previewTrava = False
        End If
        RecalcularSegmentosPreview()
        AplicarModoPreview()
        AtualizarSegmentosVisuais()
    End Sub

    Private Sub LimparPreviewCrono()
        If verTodos Then DefinirVerTodos(False)
        previewTrava = False
        MostrarPreviewCrono(Nothing)
    End Sub

    ' Temporizador próprio da trava (independente do relógio da barra de tempo)
    Private WithEvents timerPreview As New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(30)}

    Private Sub AplicarModoPreview()
        If previewTrava AndAlso previewEntry IsNot Nothing Then
            timerPreview.Start()
        Else
            timerPreview.Stop()
        End If
    End Sub

    Private Sub TimerPreview_Tick(sender As Object, e As EventArgs) Handles timerPreview.Tick
        If Not previewTrava OrElse previewEntry Is Nothing OrElse segmentosPreview.Count = 0 Then Return
        If mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue OrElse mediaClock.IsPaused Then Return
        If isDraggingSlider OrElse Environment.TickCount - ultimoSaltoPreviewTick < 250 Then Return
        Dim alvo = SaltoPreview(mediaClock.CurrentTime.Value.TotalSeconds)
        If alvo >= 0 Then
            ultimoSaltoPreviewTick = Environment.TickCount
            IrParaTempo(alvo)
        End If
    End Sub

    Private Sub BtnVisualizarCrono_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = TryCast(sender, Button)
        Dim entry = If(btn IsNot Nothing, TryCast(btn.DataContext, CronoAnaliseEntry), Nothing)
        If entry Is Nothing Then Return
        e.Handled = True
        AlternarVisualizacaoCrono(entry)
    End Sub

    ' Liga/desliga a visualização (trava) dos trechos da linha; usado pelo botão da linha e pelo atalho
    Private Sub AlternarVisualizacaoCrono(entry As CronoAnaliseEntry)
        If entry Is Nothing Then Return
        If TemCortesNoVideo() Then Return
        If verTodos Then DefinirVerTodos(False) ' a visão geral dá lugar à visualização da linha

        If Object.ReferenceEquals(entry, previewEntry) AndAlso previewTrava Then
            ' Desliga a trava (os trechos continuam visíveis na timeline)
            previewTrava = False
            entry.Visualizando = False
            AplicarModoPreview()
            AtualizarSegmentosVisuais() ' timeline volta ao vídeo inteiro
            Return
        End If

        previewTrava = True
        MostrarPreviewCrono(entry)
        entry.Visualizando = True
        AplicarModoPreview()
        AtualizarSegmentosVisuais() ' timeline passa a cobrir só os trechos da linha

        If segmentosPreview.Count = 0 OrElse mediaClock Is Nothing Then Return
        IrParaTempo(segmentosPreview(0).Inicio)
        ultimoSaltoPreviewTick = Environment.TickCount
    End Sub

    ' Com a trava ativa: se a posição está fora dos trechos, devolve o início do próximo trecho (ou do primeiro, em loop); senão -1
    Private Function SaltoPreview(pos As Double) As Double
        ' Se um bloco foi clicado (travado), só ele toca; senão tocam todos os blocos da linha
        Dim alvos As List(Of SegmentoCorte) = segmentosPreview
        If segmentoTravado IsNot Nothing AndAlso segmentosPreview.Contains(segmentoTravado) Then
            alvos = New List(Of SegmentoCorte) From {segmentoTravado}
        End If
        For Each seg In alvos
            If pos >= seg.Inicio - 0.05 AndAlso pos < seg.Fim - 0.03 Then Return -1
        Next
        For Each seg In alvos
            If seg.Inicio > pos Then Return seg.Inicio
        Next
        Return alvos(0).Inicio
    End Function

    ' --- ANÁLISE ESTATÍSTICA ---
    Private Sub AtualizarAnaliseEstatistica()
        ' Agrupar por operação e calcular média e desvio padrão
        Dim grupos = cronoEntries.Where(Function(e) e.DuracaoSegundos > 0).GroupBy(Function(e) e.Operacao?.Trim()?.ToUpper())

        For Each grupo In grupos
            If String.IsNullOrWhiteSpace(grupo.Key) Then Continue For

            ' Calcular média do grupo
            Dim duracoes = grupo.Select(Function(e) e.DuracaoSegundos).ToList()
            If duracoes.Count < 1 Then Continue For

            Dim media As Double = duracoes.Average()

            ' Calcular desvio padrão
            Dim desvioPadrao As Double = 0
            If duracoes.Count > 1 Then
                Dim variancia = duracoes.Sum(Function(d) Math.Pow(d - media, 2)) / duracoes.Count
                desvioPadrao = Math.Sqrt(variancia)
            End If

            ' Atualizar cada entry do grupo
            For Each entry In grupo
                Dim duracao = entry.DuracaoSegundos

                ' Calcular desvio percentual em relação à média
                Dim desvioPerc As Double = 0
                If media > 0 Then
                    desvioPerc = ((duracao - media) / media) * 100.0
                End If

                entry.DesvioPercentual = desvioPerc

                ' Largura da barra proporcional ao desvio percentual: eixo central = 0%, extremos = -100% e +100%.
                ' Acima de 100% (em módulo) a barra fica no limite; o valor real continua no texto.
                Dim larguraMaxima As Double = 80 ' pixels (igual à largura fixa da trilha no XAML)
                Dim larguraBarra As Double = Math.Min(Math.Abs(desvioPerc), 100.0) / 100.0 * (larguraMaxima / 2)
                If larguraBarra < 2 Then larguraBarra = 2 ' mínimo visível (desvio ~0%)

                entry.BarraLargura = larguraBarra

                ' Definir cor baseada no desvio
                Dim cor As Brush
                If Math.Abs(desvioPerc) <= 5 Then
                    ' Verde: dentro de 5% da média (muito bom)
                    cor = New SolidColorBrush(Color.FromRgb(&H4C, &HAF, &H50))
                ElseIf Math.Abs(desvioPerc) <= 15 Then
                    ' Amarelo: entre 5% e 15% (aceitável)
                    cor = New SolidColorBrush(Color.FromRgb(&HFF, &HC1, &H7))
                ElseIf Math.Abs(desvioPerc) <= 30 Then
                    ' Laranja: entre 15% e 30% (atenção)
                    cor = New SolidColorBrush(Color.FromRgb(&HFF, &H98, &H0))
                Else
                    ' Vermelho: mais de 30% (dispersão alta)
                    cor = New SolidColorBrush(Color.FromRgb(&HF4, &H43, &H36))
                End If

                entry.BarraCorBrush = cor
            Next
        Next

        ' Entries sem grupo (operação vazia ou única)
        For Each entry In cronoEntries
            If String.IsNullOrWhiteSpace(entry.Operacao) OrElse entry.DuracaoSegundos = 0 Then
                entry.DesvioPercentual = 0
                entry.BarraLargura = 0
                entry.BarraCorBrush = Brushes.Gray
            End If
        Next
    End Sub

    ' --- CACHE DE CRONOANÁLISE ---
    Private cacheGlobal As CacheGlobal = Nothing
    Private caminhoCache As String = Path.Combine(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EditorDeVideos"),
        "crono_cache.json")

    ' --- PAINEL DOCKABLE ---
    Private cronoPanelWindow As CronoPanelWindow = Nothing
    Private isDraggingCronoHeader As Boolean = False
    Private dragStartPoint As Point

    Private Function TemVideoAtivo() As Boolean
        Return videoAtual IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(videoAtual.Caminho)
    End Function

    Private _isLoading As Boolean = True

    Public Sub New()
        InitializeComponent()
        GlobalFFOptions.Configure(New FFOptions With {.BinaryFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg")})
        cronometroVideo.Interval = TimeSpan.FromMilliseconds(200)
        AddHandler cronometroVideo.Tick, AddressOf CronometroTick

        ' Inicializar modo cronoanálise
        gridCrono.ItemsSource = cronoEntries
        AddHandler cronoEntries.CollectionChanged, Sub(s, ev)
                                                       ' Linha removida (ou lista limpa) que estava em pré-visualização
                                                       If previewEntry IsNot Nothing AndAlso Not cronoEntries.Contains(previewEntry) Then LimparPreviewCrono()
                                                       ' Ícone da galeria assim que há linha no grid; e bloqueio dos cortes
                                                       If videoAtual IsNot Nothing Then videoAtual.TemCronoAnalise = (cronoEntries.Count > 0)
                                                       AtualizarBloqueiosCronoCorte()
                                                       If ev.OldItems IsNot Nothing Then
                                                           For Each o In ev.OldItems
                                                               RemoveHandler DirectCast(o, CronoAnaliseEntry).PropertyChanged, AddressOf EntryTodos_PropertyChanged
                                                           Next
                                                       End If
                                                       If ev.NewItems IsNot Nothing Then
                                                           For Each n In ev.NewItems
                                                               AddHandler DirectCast(n, CronoAnaliseEntry).PropertyChanged, AddressOf EntryTodos_PropertyChanged
                                                           Next
                                                       End If
                                                       If verTodos Then
                                                           RecalcularSegmentosTodos()
                                                           segmentoTravado = Nothing
                                                           AtualizarSegmentosVisuais()
                                                       End If
                                                   End Sub

        ' Carregar cache de cronoanálise
        CarregarCache()


        ' Atualizar tooltips dos botões com os atalhos configurados
        AtualizarTooltipsAtalhos()

        ' Configurar fechamento da janela para salvar cache
        AddHandler Me.Closing, AddressOf MainWindow_Closing

        _isLoading = False
    End Sub

    ' --- MÉTODOS DE CACHE ---
    Private Sub CarregarCache()
        Try
            If File.Exists(caminhoCache) Then
                Dim json = File.ReadAllText(caminhoCache)
                Dim options = New JsonSerializerOptions() With {.PropertyNameCaseInsensitive = True}
                cacheGlobal = JsonSerializer.Deserialize(Of CacheGlobal)(json, options)
            Else
                cacheGlobal = New CacheGlobal()
            End If
        Catch ex As Exception
            MessageBox.Show("Erro ao carregar cache: " & ex.Message)
            cacheGlobal = New CacheGlobal()
        End Try
    End Sub

    Private Sub SalvarCache()
        Try
            Dim options = New JsonSerializerOptions() With {.WriteIndented = True, .PropertyNameCaseInsensitive = True}
            Dim json = JsonSerializer.Serialize(cacheGlobal, options)
            File.WriteAllText(caminhoCache, json, Text.Encoding.UTF8)
        Catch ex As Exception
            MessageBox.Show("Erro ao salvar cache: " & ex.Message)
        End Try
    End Sub

    Private Function ObterCacheVideo(caminhoVideo As String) As VideoCacheData
        If String.IsNullOrWhiteSpace(caminhoVideo) Then
            Return Nothing
        End If

        If cacheGlobal Is Nothing Then
            cacheGlobal = New CacheGlobal()
        End If

        Dim cache = cacheGlobal.Videos.FirstOrDefault(Function(v) v.CaminhoVideo = caminhoVideo)
        If cache Is Nothing Then
            cache = New VideoCacheData() With {
                .CaminhoVideo = caminhoVideo,
                .NomeVideo = Path.GetFileName(caminhoVideo)
            }
            cacheGlobal.Videos.Add(cache)
        End If
        Return cache
    End Function

    Private Sub CarregarCronoAnaliseDoVideo(videoTarefa As VideoTarefa)
        cronoEntries.Clear()

        If videoTarefa Is Nothing OrElse String.IsNullOrWhiteSpace(videoTarefa.Caminho) Then Return

        Dim cache = ObterCacheVideo(videoTarefa.Caminho)
        If cache Is Nothing Then Return

        For Each cronoDados In cache.CronoAnalises
            Dim entry As New CronoAnaliseEntry() With {
                .Operacao = cronoDados.Operacao,
                .Cliente = cronoDados.Cliente,
                .NumeroAmostras = cronoDados.NumeroAmostras,
                .Inicio1 = cronoDados.Inicio1,
                .Fim1 = cronoDados.Fim1,
                .Inicio2 = cronoDados.Inicio2,
                .Fim2 = cronoDados.Fim2,
                .Inicio3 = cronoDados.Inicio3,
                .Fim3 = cronoDados.Fim3,
                .Inicio4 = cronoDados.Inicio4,
                .Fim4 = cronoDados.Fim4
            }
            cronoEntries.Add(entry)
        Next

        videoTarefa.TemCronoAnalise = (cache.CronoAnalises.Count > 0)
        AtualizarPodeVisualizar()

        ' Atualizar análise estatística após carregar dados
        AtualizarAnaliseEstatistica()
    End Sub

    Private Sub SalvarCronoAnaliseDoVideo(videoTarefa As VideoTarefa)
        If videoTarefa Is Nothing OrElse String.IsNullOrWhiteSpace(videoTarefa.Caminho) Then Return

        Dim cache = ObterCacheVideo(videoTarefa.Caminho)
        If cache Is Nothing Then Return
        cache.CronoAnalises.Clear()

        For Each entry In cronoEntries
            Dim cronoDados As New CronoAnaliseCacheEntry() With {
                .Operacao = entry.Operacao,
                .Cliente = entry.Cliente,
                .NumeroAmostras = entry.NumeroAmostras,
                .Inicio1 = entry.Inicio1,
                .Fim1 = entry.Fim1,
                .Inicio2 = entry.Inicio2,
                .Fim2 = entry.Fim2,
                .Inicio3 = entry.Inicio3,
                .Fim3 = entry.Fim3,
                .Inicio4 = entry.Inicio4,
                .Fim4 = entry.Fim4
            }
            cache.CronoAnalises.Add(cronoDados)
        Next

        cache.TemCronoAnalise = (cronoEntries.Count > 0)
        videoTarefa.TemCronoAnalise = cache.TemCronoAnalise
        SalvarCache()
    End Sub

    ' Fechar durante uma exportação: cancela primeiro e só fecha quando a conversão parar
    Private fecharAposCancelar As Boolean = False
    ' Arquivo que o ffmpeg está gravando agora (apagado se a conversão for cancelada no meio)
    Private arquivoEmEscrita As String = ""

    Private Sub MainWindow_Closing(sender As Object, e As ComponentModel.CancelEventArgs)
        If estaProcessando Then
            e.Cancel = True
            If fecharAposCancelar Then Return   ' já está cancelando; fecha sozinho ao terminar

            Dim confirmar = ConfirmacaoWindow.Perguntar(Me,
                "Cancelar a exportação e fechar?",
                "Há uma exportação em andamento. Se fechar agora, o vídeo que está sendo convertido fica incompleto e é descartado; os que já terminaram continuam salvos.",
                "Cancelar e fechar", "Continuar exportando")
            If Not confirmar Then Return

            fecharAposCancelar = True
            btnProcessar.Content = "Cancelando…"
            btnProcessar.IsEnabled = False
            If cts IsNot Nothing Then cts.Cancel()
            Return
        End If

        ' Salvar cronoanálise do vídeo atual antes de fechar
        If videoAtual IsNot Nothing Then
            SalvarCronoAnaliseDoVideo(videoAtual)
        End If
        SalvarCache()

        ' Limpar buffer de frames
        LimparBufferFrames()
    End Sub

    ' --- GRAVAÇÃO AUTOMÁTICA DE SETTINGS ---
    Private Sub SalvarConfiguracoesAtuais()
        If videoAtual IsNot Nothing Then
            ' Rotação (uma só) + espelhamentos (combináveis), na ordem: girar e depois espelhar
            Dim filtros As New List(Of String)
            If rbRotDireita.IsChecked = True Then
                filtros.Add("transpose=1")
            ElseIf rbRotEsquerda.IsChecked = True Then
                filtros.Add("transpose=2")
            ElseIf rbRot180.IsChecked = True Then
                filtros.Add("transpose=2,transpose=2")
            End If
            If rbEspelharH.IsChecked = True Then filtros.Add("hflip")
            If rbEspelharV.IsChecked = True Then filtros.Add("vflip")
            videoAtual.FiltroFFmpeg = If(filtros.Count = 0, "", "-vf " & String.Join(",", filtros))
        End If
    End Sub

    ' --- EVENTOS DA GALERIA (BOTÕES + DRAG & DROP) ---

    ' Limita a 2 extrações simultâneas para não sobrecarregar o PC ao importar uma pasta inteira
    Private Shared ReadOnly semaforoMiniaturas As New SemaphoreSlim(2)

    Private Sub AdicionarVideoGaleria(caminho As String)
        Dim tarefa As New VideoTarefa() With {.Caminho = caminho}
        lstGaleria.Items.Add(tarefa)
        CarregarMiniaturaAsync(tarefa)
    End Sub

    ' Lê a duração (FFProbe) e extrai um quadro pequeno do vídeo (ffmpeg) para o card da galeria
    Private Async Sub CarregarMiniaturaAsync(tarefa As VideoTarefa)
        Await semaforoMiniaturas.WaitAsync()
        Try
            Dim duracao As Double = 0
            Dim arquivoMini As String = Path.Combine(Path.GetTempPath(), $"mini_{Guid.NewGuid()}.jpg")
            Await Task.Run(Sub()
                               Try
                                   duracao = FFProbe.Analyse(tarefa.Caminho).Duration.TotalSeconds
                               Catch
                               End Try
                               Try
                                   ' Quadro a 10% do vídeo (no máximo 2 s), para evitar telas pretas do início
                                   Dim instante = If(duracao > 0, Math.Min(2.0, duracao * 0.1), 0)
                                   Dim psi As New ProcessStartInfo() With {
                                       .FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
                                       .Arguments = $"-ss {instante.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)} -i ""{tarefa.Caminho}"" -frames:v 1 -vf ""scale=112:-2"" -q:v 4 -y ""{arquivoMini}""",
                                       .UseShellExecute = False, .CreateNoWindow = True,
                                       .RedirectStandardError = True, .RedirectStandardOutput = True}
                                   Using p = Process.Start(psi)
                                       ' Descartar a saída para o processo não travar com o buffer cheio
                                       p.BeginErrorReadLine()
                                       p.BeginOutputReadLine()
                                       p.WaitForExit(10000)
                                   End Using
                               Catch
                               End Try
                           End Sub)

            If duracao > 0 Then tarefa.DuracaoTexto = FormatarDuracaoCurta(duracao)

            If File.Exists(arquivoMini) Then
                Try
                    Dim bmp As New BitmapImage()
                    bmp.BeginInit()
                    bmp.CacheOption = BitmapCacheOption.OnLoad   ' lê tudo agora para poder apagar o arquivo
                    bmp.UriSource = New Uri(arquivoMini)
                    bmp.EndInit()
                    bmp.Freeze()
                    tarefa.Miniatura = bmp
                Catch
                End Try
                Try
                    File.Delete(arquivoMini)
                Catch
                End Try
            End If
        Finally
            semaforoMiniaturas.Release()
        End Try
    End Sub

    ' Duração no formato curto da galeria: 1:05 ou 1:02:05
    Private Shared Function FormatarDuracaoCurta(segundos As Double) As String
        Dim ts = TimeSpan.FromSeconds(Math.Round(segundos))
        If ts.TotalHours >= 1 Then Return $"{Int(ts.TotalHours)}:{ts.Minutes:00}:{ts.Seconds:00}"
        Return $"{ts.Minutes}:{ts.Seconds:00}"
    End Function

    Private Function VideoJaExiste(caminho As String) As Boolean
        For Each item In lstGaleria.Items
            Dim v = TryCast(item, VideoTarefa)
            If v IsNot Nothing AndAlso v.Caminho.Equals(caminho, StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If
        Next
        Return False
    End Function

    Private Sub BtnAbrirArquivos_Click(sender As Object, e As RoutedEventArgs)
        Dim abrir As New OpenFileDialog() With {.Multiselect = True, .Filter = "Vídeos|*.mp4;*.avi;*.mov;*.mkv;*.wmv;*.flv;*.mpeg;*.3gp;*.webm;*.m4v;*.mpg;*.xvid;*.divx;*.mxf;*.gxf;*.r3d;*.ari;*.imf;*.braw;*.dng;*.dpx;*.cin;*.yuv;*.ts;*.mts;*.m2ts;*.vob;*.evo;*.ogv;*.rm;*.rmvb"}
        If abrir.ShowDialog() = True Then
            For Each arq In abrir.FileNames
                If Not VideoJaExiste(arq) Then
                    AdicionarVideoGaleria(arq)
                End If
            Next
        End If
    End Sub

    Private Sub BtnAbrirPasta_Click(sender As Object, e As RoutedEventArgs)
        Using sel As New System.Windows.Forms.FolderBrowserDialog()
            If sel.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
                Dim ext = {".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".mpeg", ".3gp", ".webm", ".m4v", ".mpg", ".xvid", ".divx", ".mxf", ".gxf", ".r3d", ".ari", ".imf", ".braw", ".dng", ".dpx", ".cin", ".yuv", ".ts", ".mts", ".m2ts", ".vob", ".evo", ".ogv", ".rm", ".rmvb"}
                For Each arq In Directory.GetFiles(sel.SelectedPath, "*.*", SearchOption.AllDirectories)
                    If ext.Contains(Path.GetExtension(arq).ToLower()) AndAlso Not VideoJaExiste(arq) Then
                        AdicionarVideoGaleria(arq)
                    End If
                Next
            End If
        End Using
    End Sub

    ' --- ARRASTAR E SOLTAR NA JANELA TODA ---
    Private Shared ReadOnly ExtensoesVideo As String() = {".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".mpeg", ".3gp", ".webm", ".m4v", ".mpg", ".xvid", ".divx", ".mxf", ".gxf", ".r3d", ".ari", ".imf", ".braw", ".dng", ".dpx", ".cin", ".yuv", ".ts", ".mts", ".m2ts", ".vob", ".evo", ".ogv", ".rm", ".rmvb"}
    Private arrasteAceito As Boolean = False

    Private Shared Function EhVideo(caminho As String) As Boolean
        Return ExtensoesVideo.Contains(Path.GetExtension(caminho).ToLower())
    End Function

    Private Shared Function ItensArrastados(e As DragEventArgs) As String()
        If Not e.Data.GetDataPresent(DataFormats.FileDrop) Then Return New String() {}
        Return If(TryCast(e.Data.GetData(DataFormats.FileDrop), String()), New String() {})
    End Function

    ' Arquivos entrando na janela: mostra a camada de soltar por cima de tudo
    Private Sub Janela_PreviewDragEnter(sender As Object, e As DragEventArgs)
        Dim itens = ItensArrastados(e)
        If itens.Length = 0 OrElse pnlArrastar.Visibility = Visibility.Visible Then Return

        Dim videos = itens.Where(Function(f) File.Exists(f) AndAlso EhVideo(f)).Count()
        Dim pastas = itens.Where(Function(f) Directory.Exists(f)).Count()
        arrasteAceito = Not estaProcessando AndAlso (videos > 0 OrElse pastas > 0)

        Dim azul = Color.FromRgb(&H0, &H7A, &HFF), cinza = Color.FromRgb(&H8E, &H8E, &H93)
        Dim cor = If(arrasteAceito, azul, cinza)
        retAlvoArrastar.Stroke = New SolidColorBrush(cor)
        bdrIconeArrastar.Background = New SolidColorBrush(cor)
        bdrAlvoArrastar.Background = New SolidColorBrush(If(arrasteAceito, Color.FromArgb(&HF2, &HE5, &HF1, &HFF), Color.FromArgb(&HF2, &HF2, &HF2, &HF7)))
        icoArrastar.Text = If(arrasteAceito, ChrW(&HE896).ToString(), ChrW(&HE711).ToString())
        lblArrastarDica.Visibility = If(arrasteAceito, Visibility.Visible, Visibility.Collapsed)

        If estaProcessando Then
            lblArrastarTitulo.Text = "Aguarde a exportação"
            lblArrastarDetalhe.Text = "Não é possível adicionar vídeos durante a conversão."
        ElseIf Not arrasteAceito Then
            lblArrastarTitulo.Text = "Nenhum vídeo compatível"
            lblArrastarDetalhe.Text = "Arraste arquivos de vídeo ou pastas."
        Else
            lblArrastarTitulo.Text = "Solte para adicionar"
            Dim partes As New List(Of String)
            If videos > 0 Then partes.Add(If(videos = 1, "1 vídeo", $"{videos} vídeos"))
            If pastas > 0 Then partes.Add(If(pastas = 1, "1 pasta", $"{pastas} pastas"))
            lblArrastarDetalhe.Text = String.Join(" e ", partes) & " na galeria"
        End If

        pnlArrastar.Visibility = Visibility.Visible
    End Sub

    Private Sub Arrastar_DragOver(sender As Object, e As DragEventArgs)
        e.Effects = If(arrasteAceito, DragDropEffects.Copy, DragDropEffects.None)
        e.Handled = True
    End Sub

    Private Sub Arrastar_DragLeave(sender As Object, e As DragEventArgs)
        ' Só esconde quando o cursor sai de fato da janela (DragLeave também dispara entre elementos)
        Dim p = e.GetPosition(pnlArrastar)
        If p.X <= 0 OrElse p.Y <= 0 OrElse p.X >= pnlArrastar.ActualWidth OrElse p.Y >= pnlArrastar.ActualHeight Then
            pnlArrastar.Visibility = Visibility.Collapsed
        End If
    End Sub

    ' Durante um arraste o Windows não gera MouseMove; se chegou aqui, o arraste acabou (ex.: cancelado com Esc)
    Private Sub Arrastar_MouseMove(sender As Object, e As MouseEventArgs)
        pnlArrastar.Visibility = Visibility.Collapsed
    End Sub

    Private Sub Arrastar_Drop(sender As Object, e As DragEventArgs)
        pnlArrastar.Visibility = Visibility.Collapsed
        e.Handled = True
        If Not arrasteAceito Then Return

        For Each item In ItensArrastados(e)
            If Directory.Exists(item) Then
                ' Pasta: adiciona os vídeos dela e das subpastas
                Try
                    For Each arq In Directory.GetFiles(item, "*.*", SearchOption.AllDirectories)
                        If EhVideo(arq) AndAlso Not VideoJaExiste(arq) Then AdicionarVideoGaleria(arq)
                    Next
                Catch ex As Exception
                    Debug.WriteLine($"Erro ao ler a pasta arrastada: {ex.Message}")
                End Try
            ElseIf File.Exists(item) AndAlso EhVideo(item) AndAlso Not VideoJaExiste(item) Then
                AdicionarVideoGaleria(item)
            End If
        Next
    End Sub

    Private Sub LstGaleria_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        SalvarConfiguracoesAtuais()
        LimparPreviewCrono()

        ' Salvar cronoanálise do vídeo anterior
        If videoAtual IsNot Nothing Then
            SalvarCronoAnaliseDoVideo(videoAtual)
        End If

        ' Limpar buffer de frames do vídeo anterior
        LimparBufferFrames()

        If lstGaleria.SelectedItem IsNot Nothing Then
            videoAtual = DirectCast(lstGaleria.SelectedItem, VideoTarefa)

            ' Carregar cronoanálise do novo vídeo
            CarregarCronoAnaliseDoVideo(videoAtual)

            If mediaClock IsNot Nothing Then
                mediaClock.Controller.Stop()
                VisualizadorVideo.Clock = Nothing
                mediaClock = Nothing
            End If

            mediaTimeline = New MediaTimeline(New Uri(videoAtual.Caminho))
            mediaTimeline.RepeatBehavior = New RepeatBehavior(1)
            mediaTimeline.SpeedRatio = _velocidadeReproducao
            mediaClock = mediaTimeline.CreateClock(True)
            VisualizadorVideo.Clock = mediaClock
            mediaClock.Controller.Begin()

            AtualizarLabelVelocidade()

            ' Garantir que o MediaElement está visível
            VisualizadorVideo.Visibility = Visibility.Visible
            imgFrameBuffer.Visibility = Visibility.Collapsed

            AtualizarEstadoPlayPause(True)

            ' Ler de volta rotação e espelhamentos do filtro salvo (ex.: "-vf transpose=1,hflip")
            Dim partes = If(videoAtual.FiltroFFmpeg, "").Replace("-vf", "").Trim().Split(","c).Select(Function(p) p.Trim()).ToList()
            Dim qtdT2 = partes.Where(Function(p) p = "transpose=2").Count()
            rbRotDireita.IsChecked = partes.Contains("transpose=1")
            rbRotEsquerda.IsChecked = (qtdT2 = 1)
            rbRot180.IsChecked = (qtdT2 >= 2)
            rbNormal.IsChecked = Not (rbRotDireita.IsChecked = True OrElse rbRotEsquerda.IsChecked = True OrElse rbRot180.IsChecked = True)
            rbEspelharH.IsChecked = partes.Contains("hflip")
            rbEspelharV.IsChecked = partes.Contains("vflip")

            AtualizarIndicadorDeCorte()
            AtualizarSegmentosVisuais()

            ' Iniciar buffer de frames na posição inicial
            ultimaPosicaoBuffer = -1
        Else
            videoAtual = Nothing
            cronoEntries.Clear()
            AtualizarSegmentosVisuais()
        End If
        AtualizarEstadoVazioVideo()
    End Sub

    ' Sem vídeo aberto: esconde o player (evita mostrar o último quadro) e exibe a orientação no lugar
    Private Sub AtualizarEstadoVazioVideo()
        If pnlVideoVazio Is Nothing Then Return
        Dim vazio = (videoAtual Is Nothing)
        pnlVideoVazio.Visibility = If(vazio, Visibility.Visible, Visibility.Collapsed)
        If vazio Then
            VisualizadorVideo.Visibility = Visibility.Hidden
            imgFrameBuffer.Visibility = Visibility.Collapsed
        End If

        ' Cada vídeo lembra a própria rotação de visualização
        btnGirarEsquerda.IsEnabled = Not vazio
        btnGirarDireita.IsEnabled = Not vazio
        AplicarRotacaoVisualizacao(False)
    End Sub

    Private Sub BtnRemoverVideoGaleria_Click(sender As Object, e As RoutedEventArgs)
        Dim button = TryCast(sender, Button)
        If button IsNot Nothing Then
            Dim video = TryCast(button.DataContext, VideoTarefa)
            If video IsNot Nothing Then
                ' Se o vídeo removido é o que está ativo, limpar
                If videoAtual IsNot Nothing AndAlso videoAtual.Caminho.Equals(video.Caminho, StringComparison.OrdinalIgnoreCase) Then
                    If mediaClock IsNot Nothing Then
                        mediaClock.Controller.Stop()
                    End If
                    videoAtual = Nothing
                End If
                lstGaleria.Items.Remove(video)
                AtualizarEstadoVazioVideo()
            End If
        End If
    End Sub

    Private Sub BtnLimpar_Click(sender As Object, e As RoutedEventArgs)
        lstGaleria.Items.Clear()
        cronoEntries.Clear()

        ' Limpar buffer de frames
        LimparBufferFrames()

        If mediaClock IsNot Nothing Then
            mediaClock.Controller.Stop()
            VisualizadorVideo.Clock = Nothing
            VisualizadorVideo.Close()
            mediaClock = Nothing
        End If
        mediaTimeline = Nothing
        videoAtual = Nothing
        AtualizarEstadoPlayPause(False)
        AtualizarIndicadorDeCorte()
        AtualizarEstadoVazioVideo()
    End Sub

    ' --- CONTROLOS DE REPRODUÇÃO E LINHA DO TEMPO ---

    Private Sub AtualizarEstadoPlayPause(estaTocando As Boolean)
        Dim simbolo As String = If(estaTocando, ChrW(&HE769), ChrW(&HE768))
        btnPlayPause.Tag = simbolo
        If btnCronoPlayPause IsNot Nothing Then
            btnCronoPlayPause.Tag = simbolo
        End If
        estaReproduzindo = estaTocando
    End Sub

    Private Sub BtnPlayPause_Click(sender As Object, e As RoutedEventArgs)
        If mediaClock Is Nothing Then Return

        If estaReproduzindo Then
            ' Pausar
            mediaClock.Controller.Pause()
            AtualizarEstadoPlayPause(False)
        Else
            ' Reproduzir
            timerPausaPasso.Stop()
            timerRenderPasso.Stop()
            renderPassoEmAndamento = False
            renderPassoPendente = False
            mediaClock.Controller.SpeedRatio = 1
            mediaClock.Controller.Resume()
            AtualizarEstadoPlayPause(True)
        End If
    End Sub

    Private Sub BorderVideo_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        BtnPlayPause_Click(Nothing, Nothing)
    End Sub

    ' --- ROTAÇÃO DA VISUALIZAÇÃO (só na tela; a exportação usa as opções de Orientação) ---
    Private Sub BtnGirarEsquerda_Click(sender As Object, e As RoutedEventArgs)
        GirarVisualizacao(-90)
    End Sub

    Private Sub BtnGirarDireita_Click(sender As Object, e As RoutedEventArgs)
        GirarVisualizacao(90)
    End Sub

    Private Sub BtnRotacaoReset_Click(sender As Object, e As RoutedEventArgs)
        If videoAtual Is Nothing Then Return
        ' Volta pelo caminho mais curto até um múltiplo de 360 (ex.: 270 → 360, 90 → 0)
        Dim atual = videoAtual.RotacaoVisualizacao
        videoAtual.RotacaoVisualizacao = Math.Round(atual / 360.0) * 360.0
        AplicarRotacaoVisualizacao(True)
    End Sub

    Private Sub GirarVisualizacao(graus As Double)
        If videoAtual Is Nothing Then Return
        videoAtual.RotacaoVisualizacao += graus
        AplicarRotacaoVisualizacao(True)
    End Sub

    ' Aplica o ângulo do vídeo atual: com animação ao girar, direto ao trocar de vídeo
    Private Sub AplicarRotacaoVisualizacao(animar As Boolean)
        Dim alvo As Double = If(videoAtual IsNot Nothing, videoAtual.RotacaoVisualizacao, 0)

        If animar Then
            Dim anim As New DoubleAnimation(alvo, New Duration(TimeSpan.FromMilliseconds(260))) With {
                .EasingFunction = New CubicEase() With {.EasingMode = EasingMode.EaseOut}}
            rotVisualizacao.BeginAnimation(RotateTransform.AngleProperty, anim)
        Else
            rotVisualizacao.BeginAnimation(RotateTransform.AngleProperty, Nothing)
            rotVisualizacao.Angle = alvo
        End If

        ' Selo no canto do vídeo e destaque nos botões enquanto estiver girado
        Dim normalizado = ((CInt(alvo) Mod 360) + 360) Mod 360
        bdrSeloRotacao.Visibility = If(normalizado = 0, Visibility.Collapsed, Visibility.Visible)
        lblSeloRotacao.Text = If(normalizado = 180, "Girado 180°", If(normalizado = 90, "Girado 90° à direita", "Girado 90° à esquerda"))
        Dim corBotao = If(normalizado = 0, New SolidColorBrush(Color.FromRgb(&HE9, &HE9, &HEE)), New SolidColorBrush(Color.FromRgb(&HE5, &HF1, &HFF)))
        btnGirarEsquerda.Background = corBotao
        btnGirarDireita.Background = corBotao
    End Sub

    Private Sub BtnVelocidadeMenos_Click(sender As Object, e As RoutedEventArgs)
        Dim velocidades = {0.25, 0.5, 0.75, 1.0, 1.5, 2.0}
        Dim idxAtual = Array.IndexOf(velocidades, _velocidadeReproducao)
        If idxAtual > 0 Then
            _velocidadeReproducao = velocidades(idxAtual - 1)
        Else
            _velocidadeReproducao = 0.25
        End If
        RecriarClockComVelocidade()
    End Sub

    Private Sub BtnVelocidadeMais_Click(sender As Object, e As RoutedEventArgs)
        Dim velocidades = {0.25, 0.5, 0.75, 1.0, 1.5, 2.0}
        Dim idxAtual = Array.IndexOf(velocidades, _velocidadeReproducao)
        If idxAtual >= 0 AndAlso idxAtual < velocidades.Length - 1 Then
            _velocidadeReproducao = velocidades(idxAtual + 1)
        Else
            _velocidadeReproducao = 2.0
        End If
        RecriarClockComVelocidade()
    End Sub

    Private Sub RecriarClockComVelocidade()
        If videoAtual Is Nothing OrElse mediaClock Is Nothing Then
            AtualizarLabelVelocidade()
            Return
        End If

        ' Guardar estado atual (antes de parar o clock)
        Dim estavaTocando = estaReproduzindo
        Dim posicaoAtual As TimeSpan? = mediaClock.CurrentTime

        ' Parar clock atual
        mediaClock.Controller.Stop()
        VisualizadorVideo.Clock = Nothing
        mediaClock = Nothing

        ' Criar novo timeline com a nova velocidade
        mediaTimeline = New MediaTimeline(New Uri(videoAtual.Caminho))
        mediaTimeline.RepeatBehavior = New RepeatBehavior(1)
        mediaTimeline.SpeedRatio = _velocidadeReproducao
        mediaClock = mediaTimeline.CreateClock(True)
        VisualizadorVideo.Clock = mediaClock

        ' Iniciar o clock
        mediaClock.Controller.Begin()

        ' Pausar imediatamente para evitar que o vídeo comece do tempo 0
        mediaClock.Controller.Pause()

        ' Restaurar posição
        If posicaoAtual.HasValue Then
            mediaClock.Controller.Seek(posicaoAtual.Value, TimeSeekOrigin.BeginTime)
        End If

        ' Se estava a reproduzir, retomar
        If estavaTocando Then
            mediaClock.Controller.Resume()
        End If

        AtualizarLabelVelocidade()
    End Sub

    Private Sub AtualizarLabelVelocidade()
        ' Velocidade normal em cinza; fora do normal em azul, para chamar atenção sem fugir da paleta
        lblVelocidade.Text = $"{_velocidadeReproducao:F1}×"
        lblVelocidade.Foreground = New SolidColorBrush(If(_velocidadeReproducao = 1.0,
                                                           Color.FromRgb(&H86, &H86, &H8B),
                                                           Color.FromRgb(&H0, &H7A, &HFF)))
    End Sub

    Private Sub BtnMuteAudio_Checked(sender As Object, e As RoutedEventArgs)
        VisualizadorVideo.IsMuted = True
        btnMuteAudio.Tag = ChrW(&HE74F).ToString()
    End Sub

    Private Sub BtnMuteAudio_Unchecked(sender As Object, e As RoutedEventArgs)
        VisualizadorVideo.IsMuted = False
        btnMuteAudio.Tag = ChrW(&HE767).ToString()
    End Sub

    Private Sub VisualizadorVideo_MediaOpened(sender As Object, e As RoutedEventArgs)
        If VisualizadorVideo.NaturalDuration.HasTimeSpan Then
            duracaoTotalVideo = VisualizadorVideo.NaturalDuration.TimeSpan.TotalSeconds
            AplicarEscalaTimeline()
            cronometroVideo.Start()

            Try
                Dim info = FFProbe.Analyse(videoAtual.Caminho)
                If info.VideoStreams.Count > 0 Then
                    frameRate = info.VideoStreams(0).AvgFrameRate
                End If
            Catch
                frameRate = 30.0
            End Try

            AtualizarIndicadorDeCorte()
            AtualizarSegmentosVisuais()

            ' Iniciar carregamento do buffer de frames na posição inicial
            CarregarBufferFrames(0)
        End If
    End Sub

    Private Sub CronometroTick(sender As Object, e As EventArgs)
        If mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue Then Return
        ' Durante passos de frame rápidos (tecla segurada) o relógio ainda está atrasado: não sobrescrever a posição
        If Not estaReproduzindo AndAlso Environment.TickCount - ultimoPassoFrameTick < 400 Then Return
        If Not isDraggingSlider Then
            Dim pos = mediaClock.CurrentTime.Value.TotalSeconds

            ' Reprodução travada num segmento: volta ao início ao chegar no fim (ou ao sair do intervalo)
            ' (com a visualização da cronoanálise ativa quem controla é o timerPreview)
            If segmentoTravado IsNot Nothing AndAlso Not previewTrava AndAlso (pos >= segmentoTravado.Fim - 0.03 OrElse pos < segmentoTravado.Inicio - 0.5) Then
                IrParaTempo(segmentoTravado.Inicio)
                Return
            End If

            slLinhaTempo.Value = pos
            lblTempoAtual.Text = FormatarTempo(mediaClock.CurrentTime.Value.TotalSeconds)
        End If
    End Sub

    Private Sub SlLinhaTempo_DragStarted(sender As Object, e As DragStartedEventArgs)
        isDraggingSlider = True
        If mediaClock IsNot Nothing Then mediaClock.Controller.Pause()
        popupTempoDrag.IsOpen = True
    End Sub

    Private Sub SlLinhaTempo_DragCompleted(sender As Object, e As DragCompletedEventArgs)
        isDraggingSlider = False
        popupTempoDrag.IsOpen = False
        If mediaClock IsNot Nothing Then
            mediaClock.Controller.Resume()
            mediaClock.Controller.Seek(TimeSpan.FromSeconds(slLinhaTempo.Value), TimeSeekOrigin.BeginTime)
            lblTempoAtual.Text = FormatarTempo(slLinhaTempo.Value)
            If Not estaReproduzindo Then
                Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                          mediaClock.Controller.Pause()
                                                                      End Sub)
            End If

            ' Carregar buffer na nova posição após o seek
            ultimaPosicaoBuffer = slLinhaTempo.Value
            CarregarBufferFrames(slLinhaTempo.Value)
        End If
    End Sub

    Private Sub SlLinhaTempo_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If isDraggingSlider Then
            lblTempoDrag.Text = FormatarTempo(slLinhaTempo.Value)
            popupTempoDrag.HorizontalOffset += 0.1
            popupTempoDrag.HorizontalOffset -= 0.1
        End If
    End Sub

    Private Sub SlLinhaTempo_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If mediaClock Is Nothing Then Return

        ' Se o clique foi no Thumb (bolinha), deixar o evento passar para permitir arrasto
        Dim origem = TryCast(e.OriginalSource, DependencyObject)
        While origem IsNot Nothing
            If TypeOf origem Is Thumb Then
                Return ' Não Handled - deixa o Thumb processar o arrasto
            End If
            origem = VisualTreeHelper.GetParent(origem)
        End While

        ' Calcular a posição do clique em relação ao slider
        Dim posicaoMouse = e.GetPosition(slLinhaTempo)
        Dim larguraSlider = slLinhaTempo.ActualWidth

        If larguraSlider <= 0 Then Return

        ' Calcular proporção (0 a 1)
        Dim proporcao = Math.Max(0.0, Math.Min(1.0, posicaoMouse.X / larguraSlider))

        ' Converter para valor no range do slider
        Dim range = slLinhaTempo.Maximum - slLinhaTempo.Minimum
        Dim novoValor = slLinhaTempo.Minimum + (range * proporcao)

        ' Atualizar slider e seek no vídeo
        slLinhaTempo.Value = novoValor
        mediaClock.Controller.Seek(TimeSpan.FromSeconds(novoValor), TimeSeekOrigin.BeginTime)
        lblTempoAtual.Text = FormatarTempo(novoValor)

        ' Se estava pausado, manter pausado após o seek
        If Not estaReproduzindo Then
            Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                      mediaClock.Controller.Pause()
                                                                  End Sub)
        End If

        ' Carregar buffer na nova posição
        ultimaPosicaoBuffer = novoValor
        CarregarBufferFrames(novoValor)

        e.Handled = True
    End Sub

    ' --- SISTEMA VISUAL DE CORTE ---
    Private Sub BtnRegistrarTempo_Click(sender As Object, e As RoutedEventArgs)
        If TemCronoNoVideo() OrElse estaProcessando Then Return ' excludentes com cronoanálise; sem edição durante a exportação
        If videoAtual Is Nothing OrElse mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue Then Return

        Dim t = Math.Round(mediaClock.CurrentTime.Value.TotalSeconds, 2)

        If videoAtual.TempoPendente < 0 Then
            ' 1º tempo: marca onde o corte começa/termina
            videoAtual.TempoPendente = t
        Else
            ' 2º tempo: o menor dos dois é o início e o maior é o fim
            Dim a = videoAtual.TempoPendente
            If Math.Abs(t - a) < 0.01 Then Return ' mesmo instante: ignora
            videoAtual.Segmentos.Add(New SegmentoCorte() With {.Inicio = Math.Min(a, t), .Fim = Math.Max(a, t)})
            videoAtual.Segmentos.Sort(Function(x, y) x.Inicio.CompareTo(y.Inicio))
            videoAtual.TempoPendente = -1
        End If

        AtualizarIndicadorDeCorte()
        AtualizarSegmentosVisuais()
    End Sub

    ' Marcador do 1º tempo registrado (aguardando o 2º)
    Private Sub AtualizarIndicadorDeCorte()
        If videoAtual Is Nothing OrElse slLinhaTempo.Maximum <= slLinhaTempo.Minimum OrElse videoAtual.TempoPendente < 0 Then
            bdrCorte.Visibility = Visibility.Hidden
            Return
        End If

        Dim larguraTotal As Double = gridSliderCorte.ActualWidth - 12
        If larguraTotal > 0 Then
            Dim rmin = slLinhaTempo.Minimum, rmax = slLinhaTempo.Maximum
            Dim px = ((Math.Max(rmin, Math.Min(videoAtual.TempoPendente, rmax)) - rmin) / (rmax - rmin)) * larguraTotal
            bdrCorte.Width = 8
            Canvas.SetLeft(bdrCorte, px - 4)
            bdrCorte.Visibility = Visibility.Visible
        End If
    End Sub

    Private Sub GridSliderCorte_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        AtualizarIndicadorDeCorte()
        AtualizarSegmentosVisuais()
        AtualizarMarcaTempoCrono()
    End Sub

    ' Risquinho na timeline no tempo da célula de tempo (Início/Fim) selecionada na cronoanálise
    Private Sub AtualizarMarcaTempoCrono()
        Dim t As Double = -1
        If gridCrono IsNot Nothing AndAlso gridCrono.SelectedCells.Count > 0 Then
            Dim cel = gridCrono.SelectedCells(gridCrono.SelectedCells.Count - 1)
            Dim entry = TryCast(cel.Item, CronoAnaliseEntry)
            If entry IsNot Nothing AndAlso cel.Column IsNot Nothing Then
                Select Case cel.Column.DisplayIndex
                    Case 4 : t = entry.Inicio1
                    Case 5 : t = entry.Fim1
                    Case 6 : t = entry.Inicio2
                    Case 7 : t = entry.Fim2
                    Case 8 : t = entry.Inicio3
                    Case 9 : t = entry.Fim3
                    Case 10 : t = entry.Inicio4
                    Case 11 : t = entry.Fim4
                End Select
            End If
        End If

        Dim rmin = slLinhaTempo.Minimum, rmax = slLinhaTempo.Maximum
        Dim span = rmax - rmin
        Dim larguraCanvas = canvasCorte.ActualWidth
        If videoAtual Is Nothing OrElse t <= 0 OrElse span <= 0 OrElse larguraCanvas <= 0 OrElse t < rmin OrElse t > rmax Then
            bdrMarcaTempo.Visibility = Visibility.Hidden
            Return
        End If

        Canvas.SetLeft(bdrMarcaTempo, ((t - rmin) / span) * larguraCanvas - bdrMarcaTempo.Width / 2)
        bdrMarcaTempo.Visibility = Visibility.Visible
    End Sub

    ' --- CORTE MÚLTIPLO ---
    ' Alterna entre azul escuro e azul médio para manter o visual limpo
    Private Shared ReadOnly CoresSegmentos As Color() = {
        Color.FromRgb(&H1B, &H3A, &H6B), Color.FromRgb(&H3B, &H82, &HC4)}

    Private Function CorSegmento(indice As Integer) As Color
        Return CoresSegmentos(indice Mod CoresSegmentos.Length)
    End Function

    Private Sub BtnLimparSegmentos_Click(sender As Object, e As RoutedEventArgs)
        If videoAtual Is Nothing Then Return
        videoAtual.Segmentos.Clear()
        AtualizarSegmentosVisuais()
    End Sub

    Private Sub BtnRemoverSegmento_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = TryCast(sender, Button)
        Dim seg = If(btn IsNot Nothing, TryCast(btn.Tag, SegmentoCorte), Nothing)
        If seg IsNot Nothing AndAlso videoAtual IsNot Nothing Then
            videoAtual.Segmentos.Remove(seg)
            AtualizarSegmentosVisuais()
        End If
    End Sub

    ' Clique num bloco/chip: trava a reprodução no período do segmento (novo clique destrava)
    Private Sub SegmentoTag_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        Dim fe = TryCast(sender, FrameworkElement)
        Dim seg = If(fe IsNot Nothing, TryCast(fe.Tag, SegmentoCorte), Nothing)
        AlternarTravaSegmento(seg)
        e.Handled = True
    End Sub

    ' Trava/destrava a reprodução no segmento (usado pelos círculos/linhas numerados da timeline e pelos chips)
    Private Sub AlternarTravaSegmento(seg As SegmentoCorte)
        If seg Is Nothing OrElse mediaClock Is Nothing Then Return

        If Object.ReferenceEquals(seg, segmentoTravado) Then
            segmentoTravado = Nothing
        Else
            segmentoTravado = seg
            AplicarEscalaTimeline() ' ajusta os limites antes de posicionar no início do bloco
            IrParaTempo(seg.Inicio)
        End If
        AtualizarSegmentosVisuais()
    End Sub

    ' Distribui os segmentos (ordenados por início) em pistas: sobrepostos sobem um nível, os demais reaproveitam o nível
    Private Shared Function CalcularPistas(segmentos As List(Of SegmentoCorte)) As Integer()
        Dim pistas(segmentos.Count - 1) As Integer
        Dim fimPorPista As New List(Of Double)()
        For i = 0 To segmentos.Count - 1
            Dim escolhida As Integer = -1
            For p = 0 To fimPorPista.Count - 1
                If segmentos(i).Inicio >= fimPorPista(p) Then
                    escolhida = p
                    Exit For
                End If
            Next
            If escolhida = -1 Then
                fimPorPista.Add(segmentos(i).Fim)
                escolhida = fimPorPista.Count - 1
            Else
                fimPorPista(escolhida) = segmentos(i).Fim
            End If
            pistas(i) = escolhida
        Next
        Return pistas
    End Function

    Private Sub IrParaTempo(segundos As Double)
        If mediaClock Is Nothing Then Return
        slLinhaTempo.Value = segundos
        mediaClock.Controller.Seek(TimeSpan.FromSeconds(segundos), TimeSeekOrigin.BeginTime)
        lblTempoAtual.Text = FormatarTempo(segundos)
        If Not estaReproduzindo Then
            Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                      If mediaClock IsNot Nothing Then mediaClock.Controller.Pause()
                                                                  End Sub)
        End If
        ultimaPosicaoBuffer = segundos
        CarregarBufferFrames(segundos)
    End Sub

    Private duracaoTotalVideo As Double = 0

    ' A timeline tem sempre a escala do vídeo inteiro
    Private Sub AplicarEscalaTimeline()
        ' A timeline sempre representa o vídeo inteiro (blocos aparecem em escala proporcional ao total)
        Dim novoMin As Double = 0
        Dim novoMax As Double = duracaoTotalVideo
        If novoMax <= novoMin Then Return
        If slLinhaTempo.Minimum = novoMin AndAlso slLinhaTempo.Maximum = novoMax Then Return
        ' Ordem que evita coerção indevida do Slider ao mudar os limites
        If novoMin > slLinhaTempo.Maximum Then
            slLinhaTempo.Maximum = novoMax
            slLinhaTempo.Minimum = novoMin
        Else
            slLinhaTempo.Minimum = novoMin
            slLinhaTempo.Maximum = novoMax
        End If
        lblTempoTotal.Text = FormatarTempo(novoMax)
        AtualizarMarcaTempoCrono()
    End Sub

    Private Sub AtualizarSegmentosVisuais()
        ' Indicador de cortes na galeria e bloqueio da cronoanálise (só vale em vídeo já cortado)
        If videoAtual IsNot Nothing Then videoAtual.TemCortes = TemCortesNoVideo()
        AtualizarBloqueiosCronoCorte()
        ' Com cortes reais no vídeo, desabilita a visualização da cronoanálise (e desliga a que estiver ativa)
        AtualizarPodeVisualizar()
        If TemCortesNoVideo() AndAlso (previewEntry IsNot Nothing OrElse verTodos) Then
            If verTodos Then DefinirVerTodos(False)
            LimparPreviewCrono()
            Return
        End If

        ' Remove marcadores antigos (círculos/faixas) da linha do tempo principal
        For i = canvasCorte.Children.Count - 1 To 0 Step -1
            Dim fe = TryCast(canvasCorte.Children(i), FrameworkElement)
            If fe IsNot Nothing AndAlso Object.Equals(fe.Tag, "seg") Then canvasCorte.Children.RemoveAt(i)
        Next
        wrapSegmentos.Children.Clear()

        Dim ativo As Boolean = videoAtual IsNot Nothing
        Dim emPreview As Boolean = ativo AndAlso (previewEntry IsNot Nothing OrElse verTodos)
        Dim segs As List(Of SegmentoCorte) = SegmentosExibidos()
        Dim nSeg As Integer = If(ativo, segs.Count, 0)
        If Not ativo Then
            lblProximaParte.Text = "Selecione um vídeo na galeria."
            txtDicaSegmentos.Text = ""
        ElseIf verTodos Then
            lblProximaParte.Text = If(nSeg = 0, "Cronoanálise: nenhum trecho registrado", $"Cronoanálise: {nSeg} trecho(s) de {cronoEntries.Count} operação(ões)")
        ElseIf emPreview Then
            Dim nome = If(String.IsNullOrWhiteSpace(previewEntry.Operacao), "linha selecionada", previewEntry.Operacao)
            lblProximaParte.Text = If(nSeg = 0, $"Cronoanálise ""{nome}"": sem trechos registrados",
                $"Cronoanálise ""{nome}"": {nSeg} trecho(s){If(previewTrava, " · reprodução travada nos trechos", "")}")
        ElseIf videoAtual.TempoPendente >= 0 Then
            lblProximaParte.Text = $"Parte {nSeg + 1}: 1º tempo em {FormatarTempo(videoAtual.TempoPendente)}. Vá ao ponto final e clique em ✂ Marcar corte."
        Else
            lblProximaParte.Text = If(nSeg = 0, "Nenhum segmento", $"{nSeg} segmento(s)")
        End If
        txtDicaSegmentos.Text = If(ativo AndAlso Not emPreview AndAlso nSeg = 0 AndAlso videoAtual.TempoPendente < 0,
            "Sem segmentos, o vídeo é exportado inteiro. Para cortar, clique em ✂ Marcar corte no início do trecho e de novo no fim; repita para criar mais partes.", "")
        txtDicaSegmentos.Visibility = If(txtDicaSegmentos.Text = "", Visibility.Collapsed, Visibility.Visible)
        ' Destrava se o segmento travado deixou de existir (removido, outro vídeo, modo desligado)
        If segmentoTravado IsNot Nothing AndAlso (Not ativo OrElse Not segs.Contains(segmentoTravado)) Then
            segmentoTravado = Nothing
        End If
        AplicarEscalaTimeline()

        ' Abre espaço acima da linha do tempo para as pistas empilhadas
        Dim pistas As Integer() = If(ativo, CalcularPistas(segs), New Integer() {})
        Dim nPistas As Integer = If(pistas.Length > 0, pistas.Max() + 1, 0)
        Dim extraTopo As Double = If(nPistas > 0, Math.Max(0, 13 + (nPistas - 1) * 7 - 24), 0)
        gridControles.Margin = New Thickness(0, 15 + extraTopo, 0, 12)

        btnLimparSegmentos.Visibility = If(ativo AndAlso Not emPreview AndAlso segs.Count > 0, Visibility.Visible, Visibility.Collapsed)
        svSegmentos.Visibility = If(ativo AndAlso segs.Count > 0, Visibility.Visible, Visibility.Collapsed)

        If Not ativo OrElse segs.Count = 0 Then
            Return
        End If

        ' Chips de edição (um por segmento, com botão de remover)
        For i = 0 To segs.Count - 1
            Dim seg = segs(i)
            Dim cor = CorSegmento(i)

            Dim txt As New TextBlock() With {
                .Text = $"{i + 1}:  {FormatarTempo(seg.Inicio)} → {FormatarTempo(seg.Fim)}  ({FormatarTempo(seg.Duracao)})",
                .VerticalAlignment = VerticalAlignment.Center, .FontSize = 11, .Foreground = Brushes.White}
            Dim btnX As New Button() With {
                .Content = ChrW(&HE711), .FontFamily = New FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), .Width = 18, .Height = 18, .FontSize = 9, .Margin = New Thickness(6, 0, 0, 0),
                .Foreground = Brushes.White, .Background = Brushes.Transparent, .BorderThickness = New Thickness(0),
                .Cursor = Cursors.Hand, .Tag = seg, .ToolTip = "Remover este segmento"}
            AddHandler btnX.Click, AddressOf BtnRemoverSegmento_Click
            Dim painel As New StackPanel() With {.Orientation = Orientation.Horizontal}
            painel.Children.Add(txt)
            If Not emPreview Then painel.Children.Add(btnX) ' trechos da cronoanálise não são removidos por aqui
            Dim chip As New Border() With {
                .Background = New SolidColorBrush(cor), .CornerRadius = New CornerRadius(10), .Cursor = Cursors.Hand, .Tag = seg,
                .BorderBrush = Brushes.Black, .BorderThickness = New Thickness(If(Object.ReferenceEquals(seg, segmentoTravado), 2, 0)),
                .ToolTip = "Clique para travar/destravar a reprodução neste segmento",
                .Padding = New Thickness(10, 2, 4, 2), .Margin = New Thickness(0, 0, 6, 4), .Child = painel}
            AddHandler chip.MouseLeftButtonDown, AddressOf SegmentoTag_MouseLeftButtonDown
            wrapSegmentos.Children.Add(chip)
        Next

        Dim rmin = slLinhaTempo.Minimum, rmax = slLinhaTempo.Maximum
        Dim selecionadas As New HashSet(Of CronoAnaliseEntry)()
        If verTodos Then
            For Each cel In gridCrono.SelectedCells
                Dim en = TryCast(cel.Item, CronoAnaliseEntry)
                If en IsNot Nothing Then selecionadas.Add(en)
            Next
        End If
        Dim span = rmax - rmin
        Dim larguraCanvas = canvasCorte.ActualWidth
        If span <= 0 OrElse larguraCanvas <= 0 Then
            Return
        End If

        For i = 0 To segs.Count - 1
            Dim seg = segs(i)
            Dim travado As Boolean = Object.ReferenceEquals(seg, segmentoTravado)
            ' Com um bloco travado, só ele fica na cor padrão; os demais ficam quase da cor do fundo (discretos)
            Dim cor = CorSegmento(i)
            If verTodos Then
                ' cada operação mantém a sua cor; sem seleção todas aparecem coloridas, com seleção só as selecionadas
                cor = CorSegmento(Math.Max(0, cronoEntries.IndexOf(seg.Dono)))
                If selecionadas.Count > 0 AndAlso Not selecionadas.Contains(seg.Dono) Then cor = Color.FromRgb(&HC7, &HC7, &HCC)
            ElseIf segmentoTravado IsNot Nothing AndAlso Not travado Then
                cor = Color.FromRgb(&HEE, &HEE, &HF1)
            End If
            Dim fim = Math.Min(seg.Fim, rmax)
            Dim inicio = Math.Max(seg.Inicio, rmin)
            If fim <= inicio Then Continue For ' fora da faixa visível da timeline
            Dim pxInicio = ((inicio - rmin) / span) * larguraCanvas
            Dim pxFim = ((fim - rmin) / span) * larguraCanvas
            Dim largura = Math.Max(2, pxFim - pxInicio)
            Dim dica = $"{If(String.IsNullOrWhiteSpace(seg.Rotulo), "", seg.Rotulo & " · ")}Parte {i + 1}: {FormatarTempo(seg.Inicio)} → {FormatarTempo(seg.Fim)} (duração {FormatarTempo(seg.Duracao)})"

            ' Linha fina com a largura do segmento; cada segmento ocupa uma "pista" empilhada acima da linha do tempo
            Dim centroPista As Double = -4 - (pistas(i) * 7)
            Dim linha As New Border() With {
                .Width = largura, .Height = 4, .CornerRadius = New CornerRadius(2), .Tag = "seg", .Cursor = Cursors.Hand, .ToolTip = dica & vbCrLf & "Clique para travar/destravar a reprodução neste segmento",
                .Background = New SolidColorBrush(cor)}
            Canvas.SetLeft(linha, pxInicio)
            Canvas.SetTop(linha, centroPista - 2)
            Dim segClique = seg
            AddHandler linha.MouseLeftButtonDown, Sub(s, ev)
                                                      AlternarTravaSegmento(segClique)
                                                      ev.Handled = True
                                                  End Sub
            canvasCorte.Children.Add(linha)

            ' Círculo numerado na pista do segmento (centro do segmento)
            Dim circulo As New Border() With {
                .Width = 18, .Height = 18, .CornerRadius = New CornerRadius(9), .Tag = "seg", .Cursor = Cursors.Hand, .ToolTip = dica & vbCrLf & "Clique para travar/destravar a reprodução neste segmento",
                .Background = New SolidColorBrush(cor), .BorderBrush = If(travado, Brushes.Black, Brushes.White),
                .BorderThickness = New Thickness(If(travado, 2, 1.5)),
                .Child = New TextBlock() With {.Text = (i + 1).ToString(), .Foreground = Brushes.White, .FontSize = 10,
                    .FontWeight = FontWeights.Bold, .HorizontalAlignment = HorizontalAlignment.Center, .VerticalAlignment = VerticalAlignment.Center}}
            Canvas.SetLeft(circulo, pxInicio + (largura / 2) - 9)
            Canvas.SetTop(circulo, centroPista - 9)
            AddHandler circulo.MouseLeftButtonDown, Sub(s, ev)
                                                        AlternarTravaSegmento(segClique)
                                                        ev.Handled = True
                                                    End Sub
            canvasCorte.Children.Add(circulo)
        Next

        ' Partes fora do(s) trecho(s) ativo(s) ficam quase da cor do fundo sobre a timeline (escala sempre em relação ao total):
        ' só o bloco travado ou, na visualização da cronoanálise, todos os trechos da linha
        Dim ativos As IEnumerable(Of SegmentoCorte) = Nothing
        If segmentoTravado IsNot Nothing Then
            ativos = New List(Of SegmentoCorte) From {segmentoTravado}
        ElseIf emPreview AndAlso previewTrava Then
            ativos = segs
        End If
        If ativos IsNot Nothing Then
            Dim cursor As Double = rmin
            Dim lacunas As New List(Of Double())()
            For Each seg In ativos.OrderBy(Function(x) x.Inicio)
                If seg.Inicio > cursor Then lacunas.Add({cursor, Math.Min(seg.Inicio, rmax)})
                cursor = Math.Max(cursor, seg.Fim)
            Next
            If cursor < rmax Then lacunas.Add({cursor, rmax})
            For Each lac In lacunas
                If lac(1) <= lac(0) Then Continue For
                Dim x0 = ((lac(0) - rmin) / span) * larguraCanvas
                Dim x1 = ((lac(1) - rmin) / span) * larguraCanvas
                Dim lacuna As New Border() With {
                    .Width = Math.Max(1, x1 - x0), .Height = 6, .CornerRadius = New CornerRadius(3), .Tag = "seg", .IsHitTestVisible = False,
                    .Background = New SolidColorBrush(Color.FromRgb(&HF1, &HF1, &HF4)), .Opacity = 1}
                Canvas.SetLeft(lacuna, x0)
                Canvas.SetTop(lacuna, 5)
                canvasCorte.Children.Add(lacuna)
            Next
        End If
    End Sub

    ' --- EXPORTAÇÃO: bloqueio de edição, bitrate e animação do card ---
    Private Const BITRATE_ALVO_KBPS As Double = 6000     ' bitrate usado ao reencodar
    Private Const BITRATE_LIMITE_KBPS As Double = 7000   ' até aqui (margem de erro) o vídeo já é considerado otimizado

    ' Durante a exportação ninguém altera cortes, orientação, opções de exportação nem a lista de vídeos
    Private Sub DefinirBloqueioExportacao(bloquear As Boolean)
        svConfigCompleto.IsEnabled = Not bloquear
        svConfigCompleto.Opacity = If(bloquear, 0.5, 1.0)
        painelBotoesGaleria.IsEnabled = Not bloquear
        lstGaleria.IsHitTestVisible = Not bloquear
        AtualizarBloqueiosCronoCorte()
    End Sub

    ' Vídeo com cronoanálise (a do vídeo aberto vem do grid; as demais, do cache)
    Private Function TemCronoAnalisePersistida(v As VideoTarefa) As Boolean
        If v Is Nothing Then Return False
        If videoAtual IsNot Nothing AndAlso Object.ReferenceEquals(v, videoAtual) Then Return cronoEntries.Count > 0
        If cacheGlobal Is Nothing Then Return v.TemCronoAnalise
        Dim c = cacheGlobal.Videos.FirstOrDefault(Function(x) String.Equals(x.CaminhoVideo, v.Caminho, StringComparison.OrdinalIgnoreCase))
        Return (c IsNot Nothing AndAlso c.CronoAnalises.Count > 0) OrElse v.TemCronoAnalise
    End Function

    ' Anima o card colapsando (altura e opacidade) e então remove o vídeo da lista
    Private Async Function ColapsarEremover(v As VideoTarefa) As Task
        Dim cont = TryCast(lstGaleria.ItemContainerGenerator.ContainerFromItem(v), ListBoxItem)
        If cont Is Nothing OrElse cont.ActualHeight <= 0 Then
            lstGaleria.Items.Remove(v)
            Return
        End If

        Dim tcs As New TaskCompletionSource(Of Boolean)()
        Dim alturaInicial As Double = cont.ActualHeight
        cont.Height = alturaInicial
        cont.ClipToBounds = True
        Dim duracao As New Duration(TimeSpan.FromMilliseconds(380))
        Dim suavizacao As New CubicEase() With {.EasingMode = EasingMode.EaseInOut}
        Dim animAltura As New Animation.DoubleAnimation(alturaInicial, 0, duracao) With {.EasingFunction = suavizacao}
        Dim animOpacidade As New Animation.DoubleAnimation(1, 0, duracao) With {.EasingFunction = suavizacao}
        AddHandler animAltura.Completed, Sub() tcs.TrySetResult(True)
        cont.BeginAnimation(FrameworkElement.HeightProperty, animAltura)
        cont.BeginAnimation(UIElement.OpacityProperty, animOpacidade)
        Await tcs.Task
        lstGaleria.Items.Remove(v)
    End Function

    ' --- PROCESSAMENTO EM LOTE (CANCELAMENTO, PREVENÇÃO E UX OTIMIZADOS) ---
    Private Async Sub BtnProcessar_Click(sender As Object, e As RoutedEventArgs)
        If lstGaleria.Items.Count = 0 Then Return

        If estaProcessando Then
            If cts IsNot Nothing Then
                cts.Cancel()
                btnProcessar.Content = "Cancelando…"
                btnProcessar.IsEnabled = False
            End If
            Return
        End If

        SalvarConfiguracoesAtuais()
        If videoAtual IsNot Nothing Then SalvarCronoAnaliseDoVideo(videoAtual)

        ' Vídeos com cronoanálise não são exportados nem convertidos: ficam na lista, intactos
        Dim fila As List(Of VideoTarefa) = lstGaleria.Items.Cast(Of VideoTarefa)().Where(Function(v) Not TemCronoAnalisePersistida(v)).ToList()
        Dim ignoradosCrono As Integer = lstGaleria.Items.Count - fila.Count
        If fila.Count = 0 Then
            MessageBox.Show("Todos os vídeos da lista têm cronoanálise e não são exportados.", "Nada para exportar", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        ' Tela de exportação: segmentos, destino, exclusão dos originais e desempenho
        Dim algumTemSegmentos As Boolean = fila.Any(Function(t) t IsNot Nothing AndAlso t.Segmentos.Count > 0)
        Dim cfg = ExportSettingsManager.Carregar()
        Dim telaExportar As New ExportarWindow(cfg, fila.Count, ignoradosCrono, algumTemSegmentos) With {.Owner = Me}
        If telaExportar.ShowDialog() <> True Then Return

        Dim exportarUnico As Boolean = algumTemSegmentos AndAlso telaExportar.ExportarUnico
        Dim salvarNaOriginal As Boolean = cfg.SalvarNaPastaOriginal
        Dim excluirOriginais As Boolean = cfg.DeletarOriginal
        Dim destino As String = If(salvarNaOriginal, "", telaExportar.PastaDestino)

        segmentoTravado = Nothing
        estaProcessando = True
        DefinirBloqueioExportacao(True)
        cts = New CancellationTokenSource()

        btnProcessar.Content = "Cancelar conversão"
        btnProcessar.Tag = ChrW(&HE711).ToString()
        btnProcessar.Background = New SolidColorBrush(Color.FromRgb(&HFF, &H3B, &H30))
        painelProgresso.Visibility = Visibility.Visible

        If mediaClock IsNot Nothing Then
            mediaClock.Controller.Stop()
            VisualizadorVideo.Clock = Nothing
            VisualizadorVideo.Close()
        End If
        AtualizarEstadoPlayPause(False)

        Dim argumentosDesempenho As String = ""
        If cfg.ModoEconomico Then
            Dim totalThreads = Environment.ProcessorCount
            Dim threadsEconomicas = Math.Max(1, totalThreads \ 2)
            argumentosDesempenho = $"-threads {threadsEconomicas}"
        End If

        Try
            For i = 0 To fila.Count - 1
                If cts.Token.IsCancellationRequested Then Exit For

                Dim indiceAtual As Integer = i
                Dim tarefa As VideoTarefa = fila(indiceAtual)
                tarefa.Progresso = 0
                tarefa.Exportando = True

                If Not File.Exists(tarefa.Caminho) Then
                    Throw New FileNotFoundException("Arquivo de entrada não encontrado: " & tarefa.Caminho)
                End If

                Dim pastaFfmpeg As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg")
                If Not File.Exists(Path.Combine(pastaFfmpeg, "ffmpeg.exe")) OrElse Not File.Exists(Path.Combine(pastaFfmpeg, "ffprobe.exe")) Then
                    Throw New FileNotFoundException("ffmpeg.exe/ffprobe.exe não encontrados em: " & pastaFfmpeg)
                End If

                Dim info = FFProbe.Analyse(tarefa.Caminho)

                Dim pastaSaida As String = destino
                If salvarNaOriginal Then
                    pastaSaida = Path.GetDirectoryName(tarefa.Caminho)
                End If

                Dim bitrateOriginal = info.Format.BitRate / 1000
                ' Só reduz o bitrate quando passa da margem (7000 kbps); até lá mantém o original
                Dim bitrateAlvo As Double = If(bitrateOriginal > BITRATE_LIMITE_KBPS, BITRATE_ALVO_KBPS, 0)

                ' Partes a exportar: um vídeo por segmento; sem segmentos, o vídeo inteiro
                Dim temSegmentos As Boolean = tarefa.Segmentos.Count > 0

                ' Vídeo já otimizado (ex.: já exportado por este editor): sem cortes, sem rotação/espelho, MP4/H.264 e
                ' bitrate dentro da margem → não precisa converter de novo
                Dim codecVideo As String = If(info.PrimaryVideoStream IsNot Nothing, info.PrimaryVideoStream.CodecName, "")
                Dim jaOtimizado As Boolean = bitrateOriginal > 0 AndAlso bitrateOriginal <= BITRATE_LIMITE_KBPS AndAlso
                    Not temSegmentos AndAlso String.IsNullOrEmpty(tarefa.FiltroFFmpeg) AndAlso
                    Path.GetExtension(tarefa.Caminho).Equals(".mp4", StringComparison.OrdinalIgnoreCase) AndAlso
                    String.Equals(codecVideo, "h264", StringComparison.OrdinalIgnoreCase)
                If jaOtimizado Then
                    lblStatusLote.Text = $"{indiceAtual + 1}/{fila.Count} - {Path.GetFileName(tarefa.Caminho)}: já otimizado ({CInt(bitrateOriginal)} kbps), conversão ignorada"
                    tarefa.Progresso = 1
                    Await Task.Delay(500)
                    Await ColapsarEremover(tarefa)
                    Continue For
                End If
                Dim separados As Boolean = temSegmentos AndAlso Not exportarUnico   ' um vídeo por segmento
                Dim unico As Boolean = temSegmentos AndAlso exportarUnico           ' um vídeo só com os segmentos juntos
                Dim partes As List(Of SegmentoCorte)
                If temSegmentos Then
                    partes = New List(Of SegmentoCorte)(tarefa.Segmentos)
                Else
                    partes = New List(Of SegmentoCorte) From {New SegmentoCorte() With {.Inicio = 0, .Fim = 0}}
                End If

                ' Nome dos arquivos: em outra pasta fica igual ao original; na mesma pasta leva "_editado"
                ' (o original ainda está lá) e o sufixo sai depois, se o original for excluído
                Dim nomeBase = Path.GetFileNameWithoutExtension(tarefa.Caminho)
                Dim sufixo As String = If(salvarNaOriginal, "_editado", "")
                Dim saida As String = ""
                Dim temporarios As New List(Of String)()
                ' Arquivos gerados e o nome final que devem ter sem o "_editado"
                Dim gerados As New List(Of Tuple(Of String, String))()

                If Not separados Then
                    saida = CaminhoLivre(pastaSaida, nomeBase & sufixo)
                    gerados.Add(Tuple.Create(saida, nomeBase))
                End If

                Try
                For k = 0 To partes.Count - 1
                    If cts.Token.IsCancellationRequested Then Exit For

                    Dim indiceParte As Integer = k
                    Dim parteInicio As Double = partes(k).Inicio
                    Dim parteFim As Double = partes(k).Fim

                    Dim destinoParte As String
                    If unico Then
                        destinoParte = Path.Combine(Path.GetTempPath(), $"editor_{Guid.NewGuid():N}_parte{k + 1}.mp4")
                        temporarios.Add(destinoParte)
                    ElseIf separados Then
                        saida = CaminhoLivre(pastaSaida, nomeBase & sufixo & $"_parte{k + 1}")
                        gerados.Add(Tuple.Create(saida, nomeBase & $"_parte{k + 1}"))
                        destinoParte = saida
                    Else
                        destinoParte = saida
                    End If

                    Dim duracao As TimeSpan = If(parteFim > parteInicio,
                        TimeSpan.FromSeconds(parteFim - parteInicio),
                        info.Duration - TimeSpan.FromSeconds(parteInicio))

                    Dim ultimaPorcentagem As Integer = -1

                    Dim engine = FFMpegArguments.FromFileInput(tarefa.Caminho).
                        OutputToFile(destinoParte, True, Sub(opt)
                                                      ' Codificação compatível com qualquer player (evita 10-bit/4:4:4 herdados do original)
                                                      opt.WithVideoCodec("libx264").ForcePixelFormat("yuv420p").WithAudioCodec("aac")
                                                      If bitrateAlvo > 0 Then opt.WithVideoBitrate(bitrateAlvo)
                                                      If parteInicio > 0 Then opt.Seek(TimeSpan.FromSeconds(parteInicio))
                                                      If parteFim > parteInicio Then opt.WithDuration(TimeSpan.FromSeconds(parteFim - parteInicio))

                                                      Dim argsFinais As String = tarefa.FiltroFFmpeg
                                                      If Not String.IsNullOrEmpty(argumentosDesempenho) Then
                                                          argsFinais = If(String.IsNullOrEmpty(argsFinais), argumentosDesempenho, argsFinais & " " & argumentosDesempenho)
                                                      End If

                                                      If Not String.IsNullOrEmpty(argsFinais) Then opt.WithCustomArgument(argsFinais)
                                                  End Sub).
                        NotifyOnProgress(Sub(perc)
                                             Dim porcentagemAtual As Integer = CInt(Math.Round(perc, 0))
                                             If porcentagemAtual <> ultimaPorcentagem Then
                                                 ultimaPorcentagem = porcentagemAtual
                                                 Dispatcher.BeginInvoke(Sub()
                                                                            pbProgresso.Value = perc
                                                                            tarefa.Progresso = (indiceParte + (perc / 100.0)) / partes.Count
                                                                            lblPorcentagem.Text = porcentagemAtual.ToString() & "%"
                                                                            Dim textoParte As String = If(temSegmentos, $" (parte {indiceParte + 1}/{partes.Count})", "")
                                                                            lblStatusLote.Text = $"Convertendo {indiceAtual + 1}/{fila.Count} - {Path.GetFileName(tarefa.Caminho)}{textoParte}"
                                                                        End Sub)
                                             End If
                                         End Sub, duracao)

                    arquivoEmEscrita = destinoParte
                    Dim concluiu As Boolean = Await engine.CancellableThrough(cts.Token).ProcessAsynchronously()
                    If concluiu Then arquivoEmEscrita = ""   ' arquivo completo: não apagar mesmo se cancelarem depois
                Next

                ' Um único vídeo: junta as partes (só os segmentos), descartando o restante
                If unico AndAlso Not cts.Token.IsCancellationRequested Then
                    lblStatusLote.Text = $"Juntando partes {indiceAtual + 1}/{fila.Count} - {Path.GetFileName(tarefa.Caminho)}"
                    Dim arquivosJuntar As String() = temporarios.ToArray()
                    Dim destinoFinal As String = saida
                    Await Task.Run(Sub() FFMpeg.Join(destinoFinal, arquivosJuntar))
                End If
                Finally
                    For Each tmp In temporarios
                        Try
                            If File.Exists(tmp) Then File.Delete(tmp)
                        Catch
                        End Try
                    Next
                End Try

                If excluirOriginais AndAlso Not cts.Token.IsCancellationRequested Then
                    Try
                        File.Delete(tarefa.Caminho)

                        ' Sem o original, os arquivos novos perdem o "_editado" e ficam com o nome dele
                        ' (as partes mantêm o "_parteN"). Se o nome já estiver ocupado, mantém como está.
                        For Each g In gerados
                            Dim atual = g.Item1
                            Dim final = Path.Combine(Path.GetDirectoryName(atual), g.Item2 & ".mp4")
                            If Not String.Equals(atual, final, StringComparison.OrdinalIgnoreCase) AndAlso
                               File.Exists(atual) AndAlso Not File.Exists(final) Then
                                File.Move(atual, final)
                            End If
                        Next
                    Catch exDel As Exception
                        Debug.WriteLine($"Não foi possível eliminar o original: {exDel.Message}")
                    End Try
                End If

                ' Concluído: o card fica 100% verde, colapsa e o vídeo sai da lista
                If Not cts.Token.IsCancellationRequested Then
                    tarefa.Progresso = 1
                    Await Task.Delay(350)
                    Await ColapsarEremover(tarefa)
                End If
            Next

            If Not cts.Token.IsCancellationRequested Then
                Dim msgFim As String = "Lote concluído com sucesso!"
                If ignoradosCrono > 0 Then msgFim &= vbCrLf & vbCrLf & $"{ignoradosCrono} vídeo(s) com cronoanálise não foram exportados."
                MessageBox.Show(msgFim, "Finalizado", MessageBoxButton.OK, MessageBoxImage.Information)
            End If

        Catch ex As OperationCanceledException
            If Not fecharAposCancelar Then
                MessageBox.Show("A conversão foi cancelada.", "Cancelado", MessageBoxButton.OK, MessageBoxImage.Warning)
            End If
        Catch ex As Exception
            If Not fecharAposCancelar Then
                MessageBox.Show("Erro: " & ex.ToString(), "Falha", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
        Finally
            ' Conversão interrompida no meio: o arquivo parcial não serve, então é apagado
            If Not String.IsNullOrEmpty(arquivoEmEscrita) Then
                Try
                    If File.Exists(arquivoEmEscrita) Then File.Delete(arquivoEmEscrita)
                Catch exApagar As Exception
                    Debug.WriteLine($"Não foi possível apagar o arquivo incompleto: {exApagar.Message}")
                End Try
                arquivoEmEscrita = ""
            End If

            estaProcessando = False
            DefinirBloqueioExportacao(False)
            For Each restante In fila
                restante.Exportando = False
                restante.Progresso = 0
            Next
            If cts IsNot Nothing Then
                cts.Dispose()
                cts = Nothing
            End If

            btnProcessar.IsEnabled = True
            btnProcessar.Content = "Exportar…"
            btnProcessar.Tag = ChrW(&HE8AB).ToString()
            btnProcessar.ClearValue(Button.BackgroundProperty)
            painelProgresso.Visibility = Visibility.Collapsed

            ' O usuário pediu para fechar durante a exportação: agora que parou, fecha
            If fecharAposCancelar Then
                Dim fechar = Dispatcher.BeginInvoke(Sub() Close())
            End If
        End Try
    End Sub

    ' --- CONTROLO DE FRAMES ---
    Private Sub BtnFrameAnterior_Click(sender As Object, e As RoutedEventArgs)
        AvancarFrame(-1)
    End Sub

    Private Sub BtnFrameProximo_Click(sender As Object, e As RoutedEventArgs)
        AvancarFrame(1)
    End Sub

    ' --- BUFFER DE FRAMES PARA NAVEGAÇÃO RÁPIDA ---
    Private Async Sub CarregarBufferFrames(posicaoAtual As Double)
        If videoAtual Is Nothing OrElse String.IsNullOrWhiteSpace(videoAtual.Caminho) Then Return

        ' Cancelar tarefa de buffer anterior se existir
        If bufferTaskCts IsNot Nothing Then
            bufferTaskCts.Cancel()
        End If

        bufferTaskCts = New CancellationTokenSource()
        Dim token = bufferTaskCts.Token

        Try
            Await Task.Run(Sub()
                               ' Calcular intervalo de frames para o buffer
                               Dim frameAtual As Integer = CInt(Math.Floor(posicaoAtual * frameRate))
                               Dim framesBuffer As Integer = CInt(Math.Ceiling(BUFFER_SEGUNDOS * frameRate))

                               ' Frames para carregar (1 seg antes e 1 seg depois)
                               Dim frameInicio As Integer = Math.Max(0, frameAtual - framesBuffer)
                               Dim frameFim As Integer = frameAtual + framesBuffer
                               Dim totalFrames As Integer = frameFim - frameInicio + 1

                               ' Limpar frames antigos que estão fora do range
                               SyncLock frameBufferLock
                                   Dim chavesParaRemover = frameBuffer.Keys.Where(Function(k) k < frameInicio - framesBuffer OrElse k > frameFim + framesBuffer).ToList()
                                   For Each chave In chavesParaRemover
                                       frameBuffer.Remove(chave)
                                   Next
                               End SyncLock

                               ' Verificar quantos frames já temos no buffer
                               Dim framesAusentes As New List(Of Integer)()
                               SyncLock frameBufferLock
                                   For frameNum As Integer = frameInicio To frameFim
                                       If Not frameBuffer.ContainsKey(frameNum) Then
                                           framesAusentes.Add(frameNum)
                                       End If
                                   Next
                               End SyncLock

                               If framesAusentes.Count = 0 Then Return ' Todos os frames já estão em cache

                               ' Extrair frames em lote usando FFmpeg
                               Try
                                   Dim timestampInicio As Double = frameInicio / frameRate
                                   Dim duracao As Double = totalFrames / frameRate

                                   ' Criar pasta temporária para os frames
                                   Dim tempFolder = Path.Combine(Path.GetTempPath(), $"frames_{Guid.NewGuid()}")
                                   Directory.CreateDirectory(tempFolder)

                                   ' Extrair todos os frames de uma vez (muito mais rápido!)
                                   Dim outputPattern = Path.Combine(tempFolder, "frame_%04d.png")
                                   Dim processInfo As New ProcessStartInfo() With {
                                       .FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
                                       .Arguments = $"-ss {timestampInicio.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -i ""{videoAtual.Caminho}"" -t {duracao.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)} -vf ""fps={frameRate}"" ""{outputPattern}""",
                                       .UseShellExecute = False,
                                       .CreateNoWindow = True,
                                       .RedirectStandardError = True,
                                       .RedirectStandardOutput = True
                                   }

                                   Dim process As Process = Process.Start(processInfo)
                                   If process IsNot Nothing Then
                                       ' Aguardar com timeout de 10 segundos
                                       process.WaitForExit(10000)
                                       process.Dispose()

                                       ' Carregar os frames extraídos
                                       Dim arquivosFrames = Directory.GetFiles(tempFolder, "frame_*.png").OrderBy(Function(f) f).ToList()

                                       For i As Integer = 0 To Math.Min(arquivosFrames.Count - 1, totalFrames - 1)
                                           If token.IsCancellationRequested Then Exit For

                                           Dim frameNum = frameInicio + i
                                           Dim arquivoFrame = arquivosFrames(i)

                                           If File.Exists(arquivoFrame) Then
                                               ' Carregar imagem na thread UI
                                               Dispatcher.Invoke(Sub()
                                                                     Try
                                                                         Dim bmp As New BitmapImage()
                                                                         bmp.BeginInit()
                                                                         bmp.CacheOption = BitmapCacheOption.OnLoad
                                                                         bmp.DecodePixelWidth = 320 ' Reduzir tamanho para economizar memória
                                                                         bmp.UriSource = New Uri(arquivoFrame)
                                                                         bmp.EndInit()
                                                                         bmp.Freeze() ' Permitir uso em outras threads

                                                                         SyncLock frameBufferLock
                                                                             If Not frameBuffer.ContainsKey(frameNum) Then
                                                                                 frameBuffer(frameNum) = bmp
                                                                             End If
                                                                         End SyncLock
                                                                     Catch ex As Exception
                                                                         Debug.WriteLine($"Erro ao carregar frame {frameNum}: {ex.Message}")
                                                                     End Try
                                                                 End Sub)
                                           End If
                                       Next

                                       ' Limpar pasta temporária
                                       Try
                                           Directory.Delete(tempFolder, True)
                                       Catch
                                       End Try
                                   End If
                               Catch ex As Exception
                                   Debug.WriteLine($"Erro ao extrair frames em lote: {ex.Message}")
                               End Try
                           End Sub, token)
        Catch ex As OperationCanceledException
            ' Cancelamento normal
        Catch ex As Exception
            Debug.WriteLine($"Erro no buffer de frames: {ex.Message}")
        End Try
    End Sub

    Private Sub LimparBufferFrames()
        SyncLock frameBufferLock
            frameBuffer.Clear()
        End SyncLock

        If bufferTaskCts IsNot Nothing Then
            bufferTaskCts.Cancel()
            bufferTaskCts = Nothing
        End If
    End Sub

    ' Caminho .mp4 que ainda não existe na pasta: "nome.mp4", senão "nome (2).mp4", "nome (3).mp4"...
    ' Nunca sobrescreve um arquivo (nem o próprio original, se a pasta escolhida for a dele).
    Private Shared Function CaminhoLivre(pasta As String, nome As String) As String
        Dim caminho = Path.Combine(pasta, nome & ".mp4")
        Dim n = 2
        While File.Exists(caminho)
            caminho = Path.Combine(pasta, $"{nome} ({n}).mp4")
            n += 1
        End While
        Return caminho
    End Function

    Private Function FormatarTempo(totalSegundos As Double) As String
        If totalSegundos < 0 Then totalSegundos = 0
        Dim ts = TimeSpan.FromSeconds(totalSegundos)
        Dim centesimos = CInt((ts.Milliseconds / 10))
        Dim totalMin = Int(ts.TotalMinutes)
        Return $"{totalMin:00}:{ts.Seconds:00},{centesimos:00}"
    End Function

    ' Posição-alvo do último passo de frame: com a tecla pressionada (repetição rápida) o relógio de mídia
    ' ainda não refletiu o seek anterior, então os passos seguintes partem do alvo e não de CurrentTime.
    Private posicaoAlvoFrame As Double = 0
    Private ultimoPassoFrameTick As Integer = 0
    Private WithEvents timerPausaPasso As New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(150)}
    Private WithEvents timerRenderPasso As New DispatcherTimer() With {.Interval = TimeSpan.FromMilliseconds(70)}
    Private renderPassoEmAndamento As Boolean = False
    Private renderPassoPendente As Boolean = False

    ' Velocidade "congelada" usada durante o passo de frames: o relógio fica ativo (o MediaElement só desenha
    ' o novo quadro com o relógio rodando) mas praticamente não avança.
    Private Const VELOCIDADE_CONGELADA As Double = 0.0001

    Private Sub AvancarFrame(direcao As Integer)
        If mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue Then Return

        If estaReproduzindo Then
            mediaClock.Controller.Pause()
            AtualizarEstadoPlayPause(False)
        End If

        Dim duracaoFrame As Double = 1.0 / frameRate

        Dim basePosicao As Double
        If Environment.TickCount - ultimoPassoFrameTick < 400 Then
            basePosicao = posicaoAlvoFrame
        Else
            basePosicao = mediaClock.CurrentTime.Value.TotalSeconds
        End If

        Dim novaPosicao As Double = basePosicao + (duracaoFrame * direcao)

        If novaPosicao < 0 Then novaPosicao = 0
        If novaPosicao > slLinhaTempo.Maximum Then novaPosicao = slLinhaTempo.Maximum

        posicaoAlvoFrame = novaPosicao
        ultimoPassoFrameTick = Environment.TickCount

        slLinhaTempo.Value = novaPosicao
        lblTempoAtual.Text = FormatarTempo(novaPosicao)

        ' Desenha o quadro de forma sincronizada: se ainda há um desenho em andamento, este pedido é ignorado
        ' e apenas a posição final (onde você parou) será desenhada quando o anterior terminar
        If renderPassoEmAndamento Then
            renderPassoPendente = True
        Else
            ExecutarRenderQuadro()
        End If
    End Sub

    Private Sub ExecutarRenderQuadro()
        If mediaClock Is Nothing Then Return
        renderPassoEmAndamento = True
        renderPassoPendente = False

        ' Relógio rodando em velocidade quase zero + seek: o quadro da posição-alvo é desenhado e fica parado
        mediaClock.Controller.SpeedRatio = VELOCIDADE_CONGELADA
        mediaClock.Controller.Resume()
        mediaClock.Controller.Seek(TimeSpan.FromSeconds(posicaoAlvoFrame), TimeSeekOrigin.BeginTime)

        timerRenderPasso.Stop()
        timerRenderPasso.Start()

        ' Pausa de verdade só depois que os passos terminarem (cada desenho reinicia o contador)
        timerPausaPasso.Stop()
        timerPausaPasso.Start()
    End Sub

    Private Sub TimerRenderPasso_Tick(sender As Object, e As EventArgs) Handles timerRenderPasso.Tick
        timerRenderPasso.Stop()
        renderPassoEmAndamento = False
        ' Houve pedidos durante o desenho: desenha só a posição mais recente
        If renderPassoPendente Then ExecutarRenderQuadro()
    End Sub

    Private Sub TimerPausaPasso_Tick(sender As Object, e As EventArgs) Handles timerPausaPasso.Tick
        timerPausaPasso.Stop()
        If mediaClock Is Nothing Then Return
        mediaClock.Controller.SpeedRatio = 1
        If Not estaReproduzindo Then mediaClock.Controller.Pause()
    End Sub

    ' --- MODO CRONOANÁLISE ---

    Private Sub BtnModoCrono_Checked(sender As Object, e As RoutedEventArgs)
        modoCronoAtivo = True
        painelConfigCompleto.Visibility = Visibility.Collapsed
        pnlCrono.Visibility = Visibility.Visible
        AtualizarBloqueiosCronoCorte()
    End Sub

    Private Sub BtnModoCrono_Unchecked(sender As Object, e As RoutedEventArgs)
        modoCronoAtivo = False
        AtualizarBloqueiosCronoCorte()

        ' Se o painel estiver desacoplado, fechar a janela flutuante (isso reacopla automaticamente)
        If cronoPanelWindow IsNot Nothing Then
            cronoPanelWindow.Close()
        End If

        pnlCrono.Visibility = Visibility.Collapsed
        painelConfigCompleto.Visibility = Visibility.Visible
        btnModoCrono.ClearValue(Button.BackgroundProperty)
        btnModoCrono.ClearValue(Button.ForegroundProperty)
    End Sub

    Private Sub RegistrarTempoAtual()
        If Not TemVideoAtivo() Then
            MessageBox.Show("Abra um vídeo antes de registrar a cronoanálise.", "Cronoanálise", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        If mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue Then Return
        If cronoEntries.Count = 0 Then
            ' Criar primeira operação automaticamente
            AdicionarNovaOperacao(False)
        End If

        Dim tempoSeg = mediaClock.CurrentTime.Value.TotalSeconds

        ' --- Determinar a entry (linha) e o campo alvo ---
        ' Prioridade 1: célula manualmente selecionada pelo utilizador (SelectedCells)
        Dim entry As CronoAnaliseEntry = Nothing
        Dim proxCampo As Integer = -1

        If gridCrono.SelectedCells.Count > 0 Then
            ' Usar a última célula selecionada (a mais recente)
            Dim ultimaCell = gridCrono.SelectedCells(gridCrono.SelectedCells.Count - 1)
            entry = TryCast(ultimaCell.Item, CronoAnaliseEntry)
            If ultimaCell.Column IsNot Nothing Then
                Dim di = ultimaCell.Column.DisplayIndex
                If di >= 4 AndAlso di <= 11 Then
                    proxCampo = di - 4
                End If
            End If
        End If

        ' Prioridade 2: CurrentCell (célula com foco)
        If entry Is Nothing Then
            entry = TryCast(gridCrono.CurrentItem, CronoAnaliseEntry)
        End If
        If proxCampo < 0 AndAlso gridCrono.CurrentCell.Column IsNot Nothing Then
            Dim di = gridCrono.CurrentCell.Column.DisplayIndex
            If di >= 4 AndAlso di <= 11 Then
                proxCampo = di - 4
            End If
        End If

        ' Prioridade 3: fallback para a última entry
        If entry Is Nothing Then
            If cronoEntries.Count > 0 Then
                entry = cronoEntries.Last()
            Else
                Return
            End If
        End If

        ' Prioridade 4: próximo campo vazio na entry
        If proxCampo < 0 Then
            proxCampo = entry.ObterProximoCampoVazio()
        End If

        ' Se todos os campos estão preenchidos, criar nova operação
        If proxCampo < 0 Then
            AdicionarNovaOperacao(False)
            entry = cronoEntries.Last()
            proxCampo = CronoAnaliseEntry.IDX_INICIO1
        End If

        ' Preencher o campo com o tempo atual
        entry.SetValorCampo(proxCampo, tempoSeg)

        ' Atualizar análise estatística
        AtualizarAnaliseEstatistica()

        MoverParaCampoCrono(entry, proxCampo + 1)
    End Sub

    ''' <summary>
    ''' Seleciona visualmente uma célula no grid de cronoanálise.
    ''' Apenas define CurrentCell (foco) mas também adiciona a célula a SelectedCells (destaque azul).
    ''' </summary>
    Private Sub SelecionarCelulaNoGrid(entry As CronoAnaliseEntry, colunaDataGrid As Integer)
        If entry Is Nothing Then Return
        If colunaDataGrid < 0 OrElse colunaDataGrid >= gridCrono.Columns.Count Then Return

        ' Limpar seleção visual anterior
        gridCrono.SelectedCells.Clear()

        ' Definir célula atual (foco)
        gridCrono.CurrentCell = New DataGridCellInfo(entry, gridCrono.Columns(colunaDataGrid))

        ' Adicionar à coleção SelectedCells para que fique com destaque visual (azul)
        gridCrono.SelectedCells.Add(New DataGridCellInfo(entry, gridCrono.Columns(colunaDataGrid)))

        gridCrono.ScrollIntoView(entry)
        gridCrono.Focus()
    End Sub

    Private Sub MoverParaCampoCrono(entry As CronoAnaliseEntry, campoIndice As Integer)
        If entry Is Nothing Then Return

        If campoIndice < 0 OrElse campoIndice > CronoAnaliseEntry.IDX_FIM4 Then Return

        ' +4 para saltar colunas Botão Excluir (0), Operação (1), Cliente (2) e Nº Amostras (3)
        Dim colunaDataGrid = campoIndice + 4
        If colunaDataGrid >= gridCrono.Columns.Count Then Return

        SelecionarCelulaNoGrid(entry, colunaDataGrid)
    End Sub

    Private Sub AdicionarNovaOperacao(Optional iniciarEdicaoOperacao As Boolean = True)
        If Not TemVideoAtivo() Then
            MessageBox.Show("Abra um vídeo antes de adicionar registros de cronoanálise.", "Cronoanálise", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim opNum = cronoEntries.Where(Function(e) e.Operacao.StartsWith("Op ")).Count() + 1
        Dim entry As New CronoAnaliseEntry() With {
            .Operacao = $"Op {opNum}",
            .NumeroAmostras = 1
        }
        cronoEntries.Add(entry)
        SelecionarCelulaNoGrid(entry, 1)

        ' Atualizar análise estatística
        AtualizarAnaliseEstatistica()

        If iniciarEdicaoOperacao Then
            ' Iniciar edição da célula Operação
            Dispatcher.BeginInvoke(Sub()
                                       SelecionarCelulaNoGrid(entry, 1)
                                       gridCrono.BeginEdit()
                                   End Sub)
        End If
    End Sub

    Private Sub BtnNovaOperacao_Click(sender As Object, e As RoutedEventArgs)
        AdicionarNovaOperacao()
    End Sub

    Private Function EscaparCampoCsv(valor As String) As String
        Dim texto As String = If(valor, String.Empty)
        Dim aspas As String = ChrW(34)
        Dim precisaEscapar As Boolean = texto.Contains(";") OrElse texto.Contains(aspas) OrElse texto.Contains(vbCr) OrElse texto.Contains(vbLf)

        If precisaEscapar Then
            texto = texto.Replace(aspas, aspas & aspas)
            Return aspas & texto & aspas
        End If

        Return texto
    End Function

    Private Sub BtnExcluirLinhaCrono_Click(sender As Object, e As RoutedEventArgs)
        Dim button = TryCast(sender, Button)
        If button IsNot Nothing Then
            Dim entry = TryCast(button.DataContext, CronoAnaliseEntry)
            If entry IsNot Nothing Then
                cronoEntries.Remove(entry)
                AtualizarAnaliseEstatistica()
            End If
        End If
    End Sub

    Private Sub BtnExportarCrono_Click(sender As Object, e As RoutedEventArgs)
        ' Salvar cronoanálise antes de exportar
        If videoAtual IsNot Nothing Then
            SalvarCronoAnaliseDoVideo(videoAtual)
        End If

        If cronoEntries.Count = 0 Then
            MessageBox.Show("Não há registros para exportar.", "Cronoanálise", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim save As New SaveFileDialog() With {
            .Filter = "CSV (*.csv)|*.csv",
            .FileName = $"cronoanálise_{Date.Now.ToString("yyyyMMdd_HHmmss")}.csv"
        }

        If save.ShowDialog() = True Then
            Try
                ' Atualizar análise antes de exportar
                AtualizarAnaliseEstatistica()

                Using sw As New StreamWriter(save.FileName, False, Text.Encoding.UTF8)
                    sw.WriteLine("Nome do vídeo;Caminho do vídeo;Operação;Cliente;Nº Amostras;Início1;Fim1;Início2;Fim2;Início3;Fim3;Início4;Fim4;Duração")
                    Dim nomeVideo = If(videoAtual IsNot Nothing, Path.GetFileName(videoAtual.Caminho), String.Empty)
                    Dim caminhoVideo = If(videoAtual IsNot Nothing, videoAtual.Caminho, String.Empty)
                    For Each entry In cronoEntries
                        Dim duracaoTotal As Double = Math.Max(0, entry.Fim1 - entry.Inicio1) +
                                                     Math.Max(0, entry.Fim2 - entry.Inicio2) +
                                                     Math.Max(0, entry.Fim3 - entry.Inicio3) +
                                                     Math.Max(0, entry.Fim4 - entry.Inicio4)
                        Dim linha = String.Join(";", {
                            EscaparCampoCsv(nomeVideo),
                            EscaparCampoCsv(caminhoVideo),
                            EscaparCampoCsv(entry.Operacao),
                            EscaparCampoCsv(entry.Cliente),
                            EscaparCampoCsv(entry.NumeroAmostras.ToString()),
                            If(entry.Inicio1 > 0, EscaparCampoCsv(entry.Inicio1Display), ""),
                            If(entry.Fim1 > 0, EscaparCampoCsv(entry.Fim1Display), ""),
                            If(entry.Inicio2 > 0, EscaparCampoCsv(entry.Inicio2Display), ""),
                            If(entry.Fim2 > 0, EscaparCampoCsv(entry.Fim2Display), ""),
                            If(entry.Inicio3 > 0, EscaparCampoCsv(entry.Inicio3Display), ""),
                            If(entry.Fim3 > 0, EscaparCampoCsv(entry.Fim3Display), ""),
                            If(entry.Inicio4 > 0, EscaparCampoCsv(entry.Inicio4Display), ""),
                            If(entry.Fim4 > 0, EscaparCampoCsv(entry.Fim4Display), ""),
                            If(duracaoTotal > 0, EscaparCampoCsv(duracaoTotal.ToString("F2", System.Globalization.CultureInfo.InvariantCulture).Replace(".", ",")), "")
                        })
                        sw.WriteLine(linha)
                    Next
                End Using
                MessageBox.Show("Exportado com sucesso!", "Cronoanálise", MessageBoxButton.OK, MessageBoxImage.Information)
            Catch ex As Exception
                MessageBox.Show("Erro ao exportar: " & ex.Message, "Erro", MessageBoxButton.OK, MessageBoxImage.Error)
            End Try
        End If
    End Sub

    Private Sub BtnLimparCrono_Click(sender As Object, e As RoutedEventArgs)
        If cronoEntries.Count > 0 Then
            If MessageBox.Show("Limpar todos os registros da cronoanálise?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) = MessageBoxResult.Yes Then
                cronoEntries.Clear()
            End If
        End If
    End Sub

    ' Abre o menu "Mais opções" logo abaixo do botão
    Private Sub BtnMaisCrono_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = TryCast(sender, Button)
        If btn?.ContextMenu Is Nothing Then Return
        btn.ContextMenu.PlacementTarget = btn
        btn.ContextMenu.Placement = Primitives.PlacementMode.Bottom
        btn.ContextMenu.IsOpen = True
    End Sub

    Private Sub BtnLimparCache_Click(sender As Object, e As RoutedEventArgs)
        Dim resultado = MessageBox.Show(
            "Limpar todo o cache de cronoanálise?" & vbCrLf &
            "Isso irá apagar todos os dados salvos de todos os vídeos." & vbCrLf &
            "Os registros atuais da tabela também serão limpos.",
            "Limpar Cache",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning)

        If resultado = MessageBoxResult.Yes Then
            ' Limpar dados em memória
            cronoEntries.Clear()
            cacheGlobal = New CacheGlobal()

            ' Apagar o ficheiro de cache
            Try
                If File.Exists(caminhoCache) Then
                    File.Delete(caminhoCache)
                End If
            Catch ex As Exception
                MessageBox.Show("Erro ao apagar o arquivo de cache: " & ex.Message)
            End Try

            ' Limpar flag de cronoanálise dos vídeos
            For Each video In lstGaleria.Items
                Dim vt = TryCast(video, VideoTarefa)
                If vt IsNot Nothing Then
                    vt.TemCronoAnalise = False
                End If
            Next

            MessageBox.Show("Cache limpo com sucesso!", "Concluído", MessageBoxButton.OK, MessageBoxImage.Information)
        End If
    End Sub

    Private removendoSelecaoInvalida As Boolean = False

    Private Sub GridCrono_SelectedCellsChanged(sender As Object, e As SelectedCellsChangedEventArgs)
        ' Colunas não selecionáveis (0 = botão excluir, 13 = análise): remove da seleção qualquer célula adicionada
        If Not removendoSelecaoInvalida AndAlso e.AddedCells.Any(Function(c) c.Column IsNot Nothing AndAlso (c.Column.DisplayIndex = 0 OrElse c.Column.DisplayIndex >= 13)) Then
            removendoSelecaoInvalida = True
            Try
                For Each c In e.AddedCells.Where(Function(x) x.Column IsNot Nothing AndAlso (x.Column.DisplayIndex = 0 OrElse x.Column.DisplayIndex >= 13)).ToList()
                    gridCrono.SelectedCells.Remove(c)
                Next
            Finally
                removendoSelecaoInvalida = False
            End Try
        End If

        ' Clique num registro: mostra os trechos dele na timeline como cortes
        If Not removendoSelecaoInvalida AndAlso gridCrono.SelectedCells.Count > 0 Then
            Dim ultima = TryCast(gridCrono.SelectedCells(gridCrono.SelectedCells.Count - 1).Item, CronoAnaliseEntry)
            If ultima IsNot Nothing AndAlso Not TemCortesNoVideo() Then MostrarPreviewCrono(ultima)
        End If

        ' Na visão geral, as operações selecionadas ficam na cor original e as demais em cinza
        If verTodos AndAlso Not removendoSelecaoInvalida Then AtualizarSegmentosVisuais()

        If Not removendoSelecaoInvalida Then AtualizarMarcaTempoCrono()

        Dim valores As New List(Of Double)

        For Each cellInfo In gridCrono.SelectedCells
            Dim entry = TryCast(cellInfo.Item, CronoAnaliseEntry)
            If entry Is Nothing OrElse cellInfo.Column Is Nothing Then Continue For

            Dim di = cellInfo.Column.DisplayIndex
            Dim val As Double? = Nothing

            Select Case di
                Case 4 : val = CronoAnaliseEntry.CronoParaSegundos(entry.Inicio1Display)
                Case 5 : val = CronoAnaliseEntry.CronoParaSegundos(entry.Fim1Display)
                Case 6 : val = CronoAnaliseEntry.CronoParaSegundos(entry.Inicio2Display)
                Case 7 : val = CronoAnaliseEntry.CronoParaSegundos(entry.Fim2Display)
                Case 8 : val = CronoAnaliseEntry.CronoParaSegundos(entry.Inicio3Display)
                Case 9 : val = CronoAnaliseEntry.CronoParaSegundos(entry.Fim3Display)
                Case 10 : val = CronoAnaliseEntry.CronoParaSegundos(entry.Inicio4Display)
                Case 11 : val = CronoAnaliseEntry.CronoParaSegundos(entry.Fim4Display)
                Case 12
                    Dim dur = entry.DuracaoDisplay
                    If Not String.IsNullOrWhiteSpace(dur) Then
                        Dim parts = dur.Split(","c)
                        If parts.Length = 2 Then
                            Dim sec As Double, cents As Double
                            If Double.TryParse(parts(0), sec) AndAlso Double.TryParse(parts(1), cents) Then
                                val = sec + cents / 100.0
                            End If
                        End If
                    End If
            End Select

            If val.HasValue AndAlso val.Value > 0 Then
                valores.Add(val.Value)
            End If
        Next

        If valores.Count >= 2 Then
            Dim soma = valores.Sum()
            Dim media = soma / valores.Count
            lblCronoSoma.Text = CronoAnaliseEntry.SegundosParaCrono(soma)
            lblCronoMedia.Text = CronoAnaliseEntry.SegundosParaCrono(media)
            lblCronoQtd.Text = $"({valores.Count} valores)"
            pnlCronoSelecao.Visibility = Visibility.Visible
        Else
            pnlCronoSelecao.Visibility = Visibility.Collapsed
        End If
    End Sub

    ' --- SUGESTÕES DE OPERAÇÃO (lista suspensa ao digitar) ---
    ' Por enquanto as opções vêm das operações já preenchidas neste vídeo; no futuro virão de um banco de dados.
    ' True quando a lista suspensa aberta é a do campo Cliente (a mesma lista/popup serve aos dois campos)
    Private sugestaoDeCliente As Boolean = False

    Private Function ObterOpcoesOperacao(entryEmEdicao As CronoAnaliseEntry) As List(Of String)
        Dim opcoes As New List(Of String)()
        For Each x In cronoEntries
            If x Is entryEmEdicao Then Continue For
            Dim op = If(If(sugestaoDeCliente, x.Cliente, x.Operacao), "").Trim()
            If op <> "" AndAlso Not opcoes.Contains(op, StringComparer.CurrentCultureIgnoreCase) Then
                opcoes.Add(op)
            End If
        Next
        Return opcoes
    End Function

    Private Sub MostrarSugestoesOperacao(tb As TextBox, mostrarTodas As Boolean)
        If tb Is Nothing OrElse Not tb.IsKeyboardFocused Then
            popupOperacoes.IsOpen = False
            Return
        End If

        Dim entry = TryCast(tb.DataContext, CronoAnaliseEntry)
        Dim texto = tb.Text.Trim()
        Dim lista As List(Of String) = ObterOpcoesOperacao(entry)

        If Not mostrarTodas Then
            lista = lista.
                Where(Function(o) o.IndexOf(texto, StringComparison.CurrentCultureIgnoreCase) >= 0 AndAlso
                                  Not o.Equals(texto, StringComparison.CurrentCultureIgnoreCase)).
                OrderByDescending(Function(o) o.StartsWith(texto, StringComparison.CurrentCultureIgnoreCase)).
                ToList()
        End If

        If lista.Count = 0 Then
            popupOperacoes.IsOpen = False
            Return
        End If

        lstOperacoes.ItemsSource = lista
        lstOperacoes.SelectedIndex = -1
        popupOperacoes.PlacementTarget = tb
        lstOperacoes.MinWidth = Math.Max(160, tb.ActualWidth)
        popupOperacoes.IsOpen = True
    End Sub

    Private Sub AplicarSugestaoOperacao(texto As String)
        Dim tb = TryCast(popupOperacoes.PlacementTarget, TextBox)
        popupOperacoes.IsOpen = False
        If tb Is Nothing Then Return
        tb.Text = texto
        tb.CaretIndex = tb.Text.Length
        ' Confirma a edição da célula com a opção escolhida
        Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                  gridCrono.CommitEdit(DataGridEditingUnit.Cell, True)
                                                              End Sub)
    End Sub

    Private Sub TxtOperacaoEdicao_TextChanged(sender As Object, e As TextChangedEventArgs)
        sugestaoDeCliente = False
        MostrarSugestoesOperacao(TryCast(sender, TextBox), False)
    End Sub

    Private Sub TxtClienteEdicao_TextChanged(sender As Object, e As TextChangedEventArgs)
        sugestaoDeCliente = True
        MostrarSugestoesOperacao(TryCast(sender, TextBox), False)
    End Sub

    Private Sub TxtClienteEdicao_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        sugestaoDeCliente = True
        TratarTeclaSugestoes(sender, e)
    End Sub

    Private Sub TxtOperacaoEdicao_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        sugestaoDeCliente = False
        TratarTeclaSugestoes(sender, e)
    End Sub

    Private Sub TratarTeclaSugestoes(sender As Object, e As KeyEventArgs)
        Dim tb = TryCast(sender, TextBox)

        If Not popupOperacoes.IsOpen Then
            ' Alt+Seta para baixo abre a lista com todas as opções
            If e.Key = Key.Down AndAlso (Keyboard.Modifiers And ModifierKeys.Alt) = ModifierKeys.Alt Then
                MostrarSugestoesOperacao(tb, True)
                e.Handled = True
            End If
            Return
        End If

        Select Case e.Key
            Case Key.Down
                If lstOperacoes.SelectedIndex < lstOperacoes.Items.Count - 1 Then lstOperacoes.SelectedIndex += 1
                lstOperacoes.ScrollIntoView(lstOperacoes.SelectedItem)
                e.Handled = True
            Case Key.Up
                If lstOperacoes.SelectedIndex >= 0 Then lstOperacoes.SelectedIndex -= 1
                If lstOperacoes.SelectedItem IsNot Nothing Then lstOperacoes.ScrollIntoView(lstOperacoes.SelectedItem)
                e.Handled = True
            Case Key.Enter, Key.Tab
                If lstOperacoes.SelectedItem IsNot Nothing Then
                    AplicarSugestaoOperacao(CStr(lstOperacoes.SelectedItem))
                    e.Handled = True
                Else
                    popupOperacoes.IsOpen = False
                End If
            Case Key.Escape
                popupOperacoes.IsOpen = False
                e.Handled = True
        End Select
    End Sub

    Private Sub TxtOperacaoEdicao_LostKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)
        popupOperacoes.IsOpen = False
    End Sub

    Private Sub TxtOperacaoEdicao_Unloaded(sender As Object, e As RoutedEventArgs)
        popupOperacoes.IsOpen = False
    End Sub

    Private Sub LstOperacoes_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        Dim item = GetParent(Of ListBoxItem)(TryCast(e.OriginalSource, DependencyObject))
        If item IsNot Nothing AndAlso item.Content IsNot Nothing Then
            AplicarSugestaoOperacao(CStr(item.Content))
            e.Handled = True
        End If
    End Sub

    Private Sub GridCrono_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs)
        editandoCelulaCrono = False
        popupOperacoes.IsOpen = False
        gridCrono.Focus()

        ' Atualizar análise estatística após edição
        Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                  AtualizarAnaliseEstatistica()
                                                                  AtualizarMarcaTempoCrono()
                                                              End Sub)
    End Sub

    Private Sub GridCrono_BeginningEdit(sender As Object, e As DataGridBeginningEditEventArgs)
        editandoCelulaCrono = True
    End Sub

    Private Sub GridCrono_PreviewTextInput(sender As Object, e As TextCompositionEventArgs)
        ' Validar entrada conforme a coluna em edição
        If gridCrono.CurrentCell.Column IsNot Nothing Then
            Dim colIndex = gridCrono.CurrentCell.Column.DisplayIndex
            If colIndex = 3 Then
                ' Nº Amostras: apenas dígitos
                For Each ch As Char In e.Text
                    If Not Char.IsDigit(ch) Then
                        e.Handled = True
                        Exit For
                    End If
                Next
            ElseIf colIndex >= 4 AndAlso colIndex <= 11 Then
                ' Colunas de tempo (Início1..Fim4): apenas dígitos, ':' e ','
                For Each ch As Char In e.Text
                    If Not Char.IsDigit(ch) AndAlso ch <> ":"c AndAlso ch <> ","c Then
                        e.Handled = True
                        Exit For
                    End If
                Next
            End If
        End If
    End Sub

    Private Sub GridCrono_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Delete OrElse e.Key = Key.Back Then
            ' Em edição de célula, manter comportamento padrão do TextBox
            If editandoCelulaCrono Then
                Return
            End If

            If gridCrono.SelectedCells.Count > 0 Then
                ' Agrupar células por linha para não processar a mesma entry múltiplas vezes
                Dim entriesProcessadas As New HashSet(Of CronoAnaliseEntry)()
                For Each cellInfo In gridCrono.SelectedCells
                    Dim entry = TryCast(cellInfo.Item, CronoAnaliseEntry)
                    If entry IsNot Nothing AndAlso cellInfo.Column IsNot Nothing Then
                        Dim colIndex = cellInfo.Column.DisplayIndex
                        Select Case colIndex
                            Case 0 ' Botão Excluir (ignorar)
                            Case 1 ' Operação
                                entry.Operacao = ""
                            Case 2 ' Cliente
                                entry.Cliente = ""
                            Case 3 ' Nº Amostras
                                entry.NumeroAmostras = 0
                            Case 4 ' Início1
                                entry.Inicio1 = 0
                            Case 5 ' Fim1
                                entry.Fim1 = 0
                            Case 6 ' Início2
                                entry.Inicio2 = 0
                            Case 7 ' Fim2
                                entry.Fim2 = 0
                            Case 8 ' Início3
                                entry.Inicio3 = 0
                            Case 9 ' Fim3
                                entry.Fim3 = 0
                            Case 10 ' Início4
                                entry.Inicio4 = 0
                            Case 11 ' Fim4
                                entry.Fim4 = 0
                        End Select
                        entriesProcessadas.Add(entry)
                    End If
                Next
                e.Handled = True
                ' Atualizar análise estatística se alguma entry foi modificada
                If entriesProcessadas.Count > 0 Then
                    AtualizarAnaliseEstatistica()
                End If
            End If
        End If
    End Sub

    Public Function HandleGlobalPreviewKeyDown(e As KeyEventArgs) As Boolean
        ' Se o DataGrid estiver em modo de edição de célula, não interceptar setas
        Dim editandoCelula As Boolean = modoCronoAtivo AndAlso editandoCelulaCrono

        ' Carregar configurações de atalhos
        Dim settings = SettingsManager.Carregar()

        ' Converter string da tecla para o enum Key
        Dim teclaFrameAnt As Key = CType([Enum].Parse(GetType(Key), settings.Atalhos("FrameAnterior")), Key)
        Dim teclaFrameProx As Key = CType([Enum].Parse(GetType(Key), settings.Atalhos("FrameProximo")), Key)

        ' Verificar atalho para Frame Anterior
        If e.Key = teclaFrameAnt AndAlso Not editandoCelula Then
            AvancarFrame(-1)
            Return True
        End If

        ' Verificar atalho para Próximo Frame
        If e.Key = teclaFrameProx AndAlso Not editandoCelula Then
            AvancarFrame(1)
            Return True
        End If

        If modoCronoAtivo AndAlso Not editandoCelula Then
            Dim teclaRegistrar As Key = CType([Enum].Parse(GetType(Key), settings.Atalhos("RegistrarTempo")), Key)
            Dim teclaPlayPause As Key = CType([Enum].Parse(GetType(Key), settings.Atalhos("PlayPause")), Key)
            Dim teclaNovaOp As Key = CType([Enum].Parse(GetType(Key), settings.Atalhos("NovaOperacao")), Key)

            ' Verificar atalho para Registrar Tempo
            If e.Key = teclaRegistrar Then
                RegistrarTempoAtual()
                Return True
            End If

            ' Verificar atalho para Play/Pause
            If e.Key = teclaPlayPause Then
                BtnPlayPause_Click(Nothing, Nothing)
                Return True
            End If

            ' Verificar atalho para Nova Operação
            If e.Key = teclaNovaOp Then
                AdicionarNovaOperacao()
                Return True
            End If

            ' Ver todas as operações na timeline
            If e.Key = TeclaDoAtalho(settings, "VerTodas") Then
                DefinirVerTodos(Not verTodos)
                Return True
            End If

            ' Visualizar (travar) os trechos da linha selecionada
            If e.Key = TeclaDoAtalho(settings, "VisualizarLinha") Then
                Dim entrySel As CronoAnaliseEntry = previewEntry
                If gridCrono.SelectedCells.Count > 0 Then
                    entrySel = If(TryCast(gridCrono.SelectedCells(gridCrono.SelectedCells.Count - 1).Item, CronoAnaliseEntry), entrySel)
                End If
                AlternarVisualizacaoCrono(entrySel)
                Return True
            End If
        End If

        If Not editandoCelula Then
            If e.Key = TeclaDoAtalho(settings, "VelocidadeMenos") Then
                BtnVelocidadeMenos_Click(Nothing, Nothing)
                Return True
            End If

            If e.Key = TeclaDoAtalho(settings, "VelocidadeMais") Then
                BtnVelocidadeMais_Click(Nothing, Nothing)
                Return True
            End If

            If e.Key = TeclaDoAtalho(settings, "Mudo") Then
                btnMuteAudio.IsChecked = Not (btnMuteAudio.IsChecked = True)
                Return True
            End If

            If e.Key = TeclaDoAtalho(settings, "GirarEsquerda") Then
                GirarVisualizacao(-90)
                Return True
            End If

            If e.Key = TeclaDoAtalho(settings, "GirarDireita") Then
                GirarVisualizacao(90)
                Return True
            End If
        End If

                Return False
            End Function

    ' Tecla configurada para a ação; Key.None (nunca disparada) se não houver tecla válida
    Private Shared Function TeclaDoAtalho(settings As KeyboardShortcutSettings, acao As String) As Key
        Dim nome As String = Nothing
        Dim resultado As Key
        If settings.Atalhos.TryGetValue(acao, nome) AndAlso [Enum].TryParse(nome, resultado) Then Return resultado
        Return Key.None
    End Function

    Private Sub BtnConfigAtalhos_Click(sender As Object, e As RoutedEventArgs)
        Dim settingsWin As New SettingsWindow()
        settingsWin.Owner = Me
        If settingsWin.ShowDialog() = True Then
            ' Atualizar tooltips com os novos atalhos
            AtualizarTooltipsAtalhos()
        End If
    End Sub

    Private Sub BtnPropriedades_Click(sender As Object, e As RoutedEventArgs)
        If videoAtual Is Nothing OrElse String.IsNullOrEmpty(videoAtual.Caminho) Then
            MessageBox.Show("Nenhum vídeo em reprodução.", "Propriedades", MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If
        Dim propWin As New VideoPropertiesWindow(videoAtual.Caminho, Me)
        propWin.ShowDialog()
    End Sub

    Private Sub AtualizarTooltipsAtalhos()
        Dim settings = SettingsManager.Carregar()
        btnFrameAnterior.ToolTip = $"Frame Anterior ({FormatTeclaTooltip(settings.Atalhos("FrameAnterior"))})"
        btnFrameProximo.ToolTip = $"Próximo Frame ({FormatTeclaTooltip(settings.Atalhos("FrameProximo"))})"

        If btnCronoFrameAnt IsNot Nothing Then
            btnCronoFrameAnt.ToolTip = $"Frame Anterior ({FormatTeclaTooltip(settings.Atalhos("FrameAnterior"))})"
        End If
        If btnCronoFrameProx IsNot Nothing Then
            btnCronoFrameProx.ToolTip = $"Próximo Frame ({FormatTeclaTooltip(settings.Atalhos("FrameProximo"))})"
        End If
        If btnCronoPlayPause IsNot Nothing Then
            btnCronoPlayPause.ToolTip = $"Play / Pause ({FormatTeclaTooltip(settings.Atalhos("PlayPause"))})"
        End If
        If btnCronoRegistrar IsNot Nothing Then
            btnCronoRegistrar.ToolTip = $"Registrar tempo atual ({FormatTeclaTooltip(settings.Atalhos("RegistrarTempo"))})"
        End If
            If btnNovaOperacao IsNot Nothing Then
                btnNovaOperacao.ToolTip = $"Nova Operação ({FormatTeclaTooltip(settings.Atalhos("NovaOperacao"))})"
            End If
        If btnVerTodasCrono IsNot Nothing Then
            btnVerTodasCrono.ToolTip = $"Mostrar na timeline as barras de tempo de todas as operações ({FormatTeclaTooltip(settings.Atalhos("VerTodas"))})"
        End If
        btnVelocidadeMenos.ToolTip = $"Diminuir velocidade ({FormatTeclaTooltip(settings.Atalhos("VelocidadeMenos"))})"
        btnVelocidadeMais.ToolTip = $"Aumentar velocidade ({FormatTeclaTooltip(settings.Atalhos("VelocidadeMais"))})"
        btnMuteAudio.ToolTip = $"Silenciar / ativar áudio ({FormatTeclaTooltip(settings.Atalhos("Mudo"))})"
        btnGirarEsquerda.ToolTip = $"Girar visualização para a esquerda ({FormatTeclaTooltip(settings.Atalhos("GirarEsquerda"))}){vbCrLf}Só na tela; a exportação não muda"
        btnGirarDireita.ToolTip = $"Girar visualização para a direita ({FormatTeclaTooltip(settings.Atalhos("GirarDireita"))}){vbCrLf}Só na tela; a exportação não muda"
        End Sub

    Private Shared Function FormatTeclaTooltip(tecla As String) As String
        Select Case tecla
            Case "Left" : Return "← Seta Esquerda"
            Case "Right" : Return "→ Seta Direita"
            Case "Up" : Return "↑ Seta Para Cima"
            Case "Down" : Return "↓ Seta Para Baixo"
            Case Else : Return tecla
        End Select
    End Function

    ' --- MÉTODOS PARA PAINEL DOCKABLE ---

    Private Sub BtnCronoFrameAnt_Click(sender As Object, e As RoutedEventArgs)
        AvancarFrame(-1)
    End Sub

    Private Sub BtnCronoPlayPause_Click(sender As Object, e As RoutedEventArgs)
        BtnPlayPause_Click(Nothing, Nothing)
    End Sub

    Private Sub BtnCronoFrameProx_Click(sender As Object, e As RoutedEventArgs)
        AvancarFrame(1)
    End Sub

    Private Sub BtnCronoRegistrar_Click(sender As Object, e As RoutedEventArgs)
        RegistrarTempoAtual()
    End Sub

    Private Sub CronoPanelHeader_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        isDraggingCronoHeader = True
        dragStartPoint = e.GetPosition(Me)

        ' Capturar o mouse para rastrear movimento
        If pnlCrono IsNot Nothing Then
            pnlCrono.CaptureMouse()
        End If

        ' Registrar movimento do mouse
        AddHandler Me.MouseMove, AddressOf CronoPanelHeader_MouseMove
        AddHandler Me.MouseUp, AddressOf CronoPanelHeader_MouseUp
    End Sub

    Private Sub CronoPanelHeader_MouseMove(sender As Object, e As MouseEventArgs)
        If Not isDraggingCronoHeader OrElse pnlCrono Is Nothing Then Return

        Dim currentPoint = e.GetPosition(Me)
        Dim distance = Math.Sqrt(Math.Pow(currentPoint.X - dragStartPoint.X, 2) + Math.Pow(currentPoint.Y - dragStartPoint.Y, 2))

        ' Se o usuário arrastou mais de 20 pixels, desacoplar o painel
        If distance > 20 Then
            DesacoplarCronoPanel()
            isDraggingCronoHeader = False
            pnlCrono.ReleaseMouseCapture()
            RemoveHandler Me.MouseMove, AddressOf CronoPanelHeader_MouseMove
            RemoveHandler Me.MouseUp, AddressOf CronoPanelHeader_MouseUp
        End If
    End Sub

    Private Sub CronoPanelHeader_MouseUp(sender As Object, e As MouseButtonEventArgs)
        isDraggingCronoHeader = False
        If pnlCrono IsNot Nothing Then
            pnlCrono.ReleaseMouseCapture()
        End If
        RemoveHandler Me.MouseMove, AddressOf CronoPanelHeader_MouseMove
        RemoveHandler Me.MouseUp, AddressOf CronoPanelHeader_MouseUp
    End Sub

    Private Sub DesacoplarCronoPanel()
        If pnlCrono Is Nothing OrElse cronoPanelWindow IsNot Nothing Then Return

        ' Expandir a linha do vídeo para ocupar todo o espaço
        gridConteudo.RowDefinitions(0).Height = New GridLength(1, GridUnitType.Star)
        ' Colapsar a linha inferior (config/crono)
        gridConteudo.RowDefinitions(2).Height = New GridLength(0, GridUnitType.Pixel)
        ' Esconder o GridSplitter
        gridSplitterCrono.Visibility = Visibility.Collapsed

        ' Criar janela flutuante
        cronoPanelWindow = New CronoPanelWindow(Me, pnlCrono)
        cronoPanelWindow.Show()
    End Sub

    Public Sub ReacoplarCronoPanel(cronoPanel As Border)
        If cronoPanel Is Nothing Then Return

        cronoPanelWindow = Nothing

        ' Remover o painel do pai atual (janela flutuante) antes de re-adicionar
        Dim parentPanel = VisualTreeHelper.GetParent(cronoPanel)
        If TypeOf parentPanel Is Panel Then
            CType(parentPanel, Panel).Children.Remove(cronoPanel)
        End If

        ' Restaurar as proporções 2/3 e 1/3
        gridConteudo.RowDefinitions(0).Height = New GridLength(2, GridUnitType.Star)
        gridConteudo.RowDefinitions(2).Height = New GridLength(1, GridUnitType.Star)
        gridConteudo.RowDefinitions(2).MinHeight = 170
        ' Mostrar o GridSplitter novamente
        gridSplitterCrono.Visibility = Visibility.Visible

        ' Re-adicionar o painel ao grid inferior (gridInferior)
        Grid.SetRow(cronoPanel, 0)
        Grid.SetColumn(cronoPanel, 0)
        gridInferior.Children.Add(cronoPanel)

        ' Restaurar a visibilidade se estava no modo crono
        If modoCronoAtivo Then
            cronoPanel.Visibility = Visibility.Visible
        End If
    End Sub

    Private Sub LstGaleria_MouseRightButtonUp(sender As Object, e As MouseButtonEventArgs)
        Dim item = DirectCast(lstGaleria.InputHitTest(e.GetPosition(lstGaleria)), DependencyObject)
        Dim listBoxItem = GetParent(Of ListBoxItem)(item)

        If listBoxItem IsNot Nothing Then
            lstGaleria.SelectedItem = listBoxItem.DataContext
        End If
    End Sub

    Private Function GetParent(Of T As DependencyObject)(child As DependencyObject) As T
        While child IsNot Nothing
            If TypeOf child Is T Then
                Return DirectCast(child, T)
            End If
            child = VisualTreeHelper.GetParent(child)
        End While
        Return Nothing
    End Function

    Private Sub ToggleCronoAnaliseFlag_Click(sender As Object, e As RoutedEventArgs)
        If lstGaleria.SelectedItem IsNot Nothing Then
            Dim video = DirectCast(lstGaleria.SelectedItem, VideoTarefa)
            If video.TemCronoAnalise Then
                ' Desmarcar
                If MessageBox.Show($"Remover marcação de cronoanálise para {Path.GetFileName(video.Caminho)}?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) = MessageBoxResult.Yes Then
                    Dim cache = ObterCacheVideo(video.Caminho)
                    cache.CronoAnalises.Clear()
                    cache.TemCronoAnalise = False
                    video.TemCronoAnalise = False
                    SalvarCache()
                End If
            Else
                ' Marcar manualmente
                video.TemCronoAnalise = True
                Dim cache = ObterCacheVideo(video.Caminho)
                cache.TemCronoAnalise = True
                SalvarCache()
            End If
            ' Atualizar visualização
            lstGaleria.Items.Refresh()
        End If
    End Sub

End Class