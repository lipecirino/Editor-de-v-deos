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

    Public Property Inicio As Double = 0
    Public Property Fim As Double = 0
    Public Property FiltroFFmpeg As String = ""

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

    Public ReadOnly Property NomeComFlag As String
        Get
            Return Path.GetFileName(Caminho)
        End Get
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
        Me.Width = 1000
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
        End Set
    End Property

    Public Property BarraLargura As Double
        Get
            Return _barraLargura
        End Get
        Set(value As Double)
            _barraLargura = value
            RaisePropertyChanged(NameOf(BarraLargura))
        End Set
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
    Private Const BUFFER_SEGUNDOS As Double = 3.0 ' 3 segundos para frente e para trás

    ' --- MODO CRONOANÁLISE ---
    Private modoCronoAtivo As Boolean = False
    Private cronoEntries As New ObservableCollection(Of CronoAnaliseEntry)()
    Private indiceCronoSelecionado As Integer = -1
    Private editandoCelulaCrono As Boolean = False

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

                ' Calcular largura da barra (baseado no desvio padrão)
                ' Barra vai de -50% a +50% da largura disponível
                Dim larguraMaxima As Double = 80 ' pixels
                Dim larguraBarra As Double = 0

                If desvioPadrao > 0 Then
                    ' Normalizar: 1 desvio padrão = 50% da largura
                    Dim desvioNormalizado = (duracao - media) / desvioPadrao
                    larguraBarra = Math.Min(Math.Abs(desvioNormalizado) * larguraMaxima / 2, larguraMaxima / 2)
                Else
                    ' Sem dispersão, barra pequena central
                    larguraBarra = 2
                End If

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
        GlobalFFOptions.Configure(New FFOptions With {.BinaryFolder = AppDomain.CurrentDomain.BaseDirectory})
        cronometroVideo.Interval = TimeSpan.FromMilliseconds(200)
        AddHandler cronometroVideo.Tick, AddressOf CronometroTick

        ' Inicializar modo cronoanálise
        gridCrono.ItemsSource = cronoEntries

        ' Carregar cache de cronoanálise
        CarregarCache()

        ' Carregar configurações de exportação e aplicar à UI
        CarregarExportSettings()

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

    Private Sub MainWindow_Closing(sender As Object, e As ComponentModel.CancelEventArgs)
        ' Salvar cronoanálise do vídeo atual antes de fechar
        If videoAtual IsNot Nothing Then
            SalvarCronoAnaliseDoVideo(videoAtual)
        End If
        SalvarCache()
        SalvarExportSettings()

        ' Limpar buffer de frames
        LimparBufferFrames()
    End Sub

    ' --- GRAVAÇÃO AUTOMÁTICA DE SETTINGS ---
    Private Sub SalvarConfiguracoesAtuais()
        If videoAtual IsNot Nothing Then
            Double.TryParse(txtInicio.Text, videoAtual.Inicio)
            Double.TryParse(txtFim.Text, videoAtual.Fim)

            If rbRotDireita.IsChecked = True Then
                videoAtual.FiltroFFmpeg = "-vf transpose=1"
            ElseIf rbRotEsquerda.IsChecked = True Then
                videoAtual.FiltroFFmpeg = "-vf transpose=2"
            ElseIf rbRot180.IsChecked = True Then
                videoAtual.FiltroFFmpeg = "-vf transpose=2,transpose=2"
            ElseIf rbEspelharH.IsChecked = True Then
                videoAtual.FiltroFFmpeg = "-vf hflip"
            ElseIf rbEspelharV.IsChecked = True Then
                videoAtual.FiltroFFmpeg = "-vf vflip"
            Else
                videoAtual.FiltroFFmpeg = ""
            End If
        End If
    End Sub

    ' --- EVENTOS DA GALERIA (BOTÕES + DRAG & DROP) ---
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
                    lstGaleria.Items.Add(New VideoTarefa() With {.Caminho = arq})
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
                        lstGaleria.Items.Add(New VideoTarefa() With {.Caminho = arq})
                    End If
                Next
            End If
        End Using
    End Sub

    Private Sub LstGaleria_Drop(sender As Object, e As DragEventArgs)
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            Dim arquivos As String() = CType(e.Data.GetData(DataFormats.FileDrop), String())
            Dim extensoesValidas = {".mp4", ".avi", ".mov", ".mkv", ".wmv", ".flv", ".mpeg", ".3gp", ".webm", ".m4v", ".mpg", ".xvid", ".divx", ".mxf", ".gxf", ".r3d", ".ari", ".imf", ".braw", ".dng", ".dpx", ".cin", ".yuv", ".ts", ".mts", ".m2ts", ".vob", ".evo", ".ogv", ".rm", ".rmvb"}
            For Each arq In arquivos
                If extensoesValidas.Contains(Path.GetExtension(arq).ToLower()) AndAlso Not VideoJaExiste(arq) Then
                    lstGaleria.Items.Add(New VideoTarefa() With {.Caminho = arq})
                End If
            Next
        End If
    End Sub

    Private Sub LstGaleria_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        SalvarConfiguracoesAtuais()

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

            txtInicio.Text = videoAtual.Inicio.ToString()
            txtFim.Text = videoAtual.Fim.ToString()
            rbNormal.IsChecked = (videoAtual.FiltroFFmpeg = "")
            rbRotDireita.IsChecked = (videoAtual.FiltroFFmpeg = "-vf transpose=1")
            rbRotEsquerda.IsChecked = (videoAtual.FiltroFFmpeg = "-vf transpose=2")
            rbRot180.IsChecked = (videoAtual.FiltroFFmpeg = "-vf transpose=2,transpose=2")
            rbEspelharH.IsChecked = (videoAtual.FiltroFFmpeg = "-vf hflip")
            rbEspelharV.IsChecked = (videoAtual.FiltroFFmpeg = "-vf vflip")

            AtualizarIndicadorDeCorte()

            ' Iniciar buffer de frames na posição inicial
            ultimaPosicaoBuffer = -1
        Else
            videoAtual = Nothing
            cronoEntries.Clear()
        End If
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
    End Sub

    ' --- CONTROLOS DE REPRODUÇÃO E LINHA DO TEMPO ---

    Private Sub AtualizarEstadoPlayPause(estaTocando As Boolean)
        Dim simbolo = If(estaTocando, "⏸", "▶")
        btnPlayPause.Content = simbolo
        If btnCronoPlayPause IsNot Nothing Then
            btnCronoPlayPause.Content = simbolo
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
            mediaClock.Controller.Resume()
            AtualizarEstadoPlayPause(True)
        End If
    End Sub

    Private Sub BorderVideo_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        BtnPlayPause_Click(Nothing, Nothing)
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
        If _velocidadeReproducao = 1.0 Then
            lblVelocidade.Text = "1.0×"
            lblVelocidade.Foreground = New SolidColorBrush(Color.FromRgb(&HCC, &H66, &H0))
        ElseIf _velocidadeReproducao > 1.0 Then
            lblVelocidade.Text = $"{_velocidadeReproducao:F1}×"
            lblVelocidade.Foreground = New SolidColorBrush(Color.FromRgb(&H0, &HCC, &H66))
        Else
            lblVelocidade.Text = $"{_velocidadeReproducao:F1}×"
            lblVelocidade.Foreground = New SolidColorBrush(Color.FromRgb(&HCC, &H66, &H0))
        End If
    End Sub

    Private Sub BtnMuteAudio_Checked(sender As Object, e As RoutedEventArgs)
        VisualizadorVideo.IsMuted = True
        btnMuteAudio.Content = "🔇"
        btnMuteAudio.Background = New SolidColorBrush(Color.FromRgb(&HCC, &H33, &H33))
    End Sub

    Private Sub BtnMuteAudio_Unchecked(sender As Object, e As RoutedEventArgs)
        VisualizadorVideo.IsMuted = False
        btnMuteAudio.Content = "🔊"
        btnMuteAudio.Background = New SolidColorBrush(Color.FromRgb(&H3, &H31, &H50))
    End Sub

    Private Sub VisualizadorVideo_MediaOpened(sender As Object, e As RoutedEventArgs)
        If VisualizadorVideo.NaturalDuration.HasTimeSpan Then
            slLinhaTempo.Maximum = VisualizadorVideo.NaturalDuration.TimeSpan.TotalSeconds
            lblTempoTotal.Text = FormatarTempo(VisualizadorVideo.NaturalDuration.TimeSpan.TotalSeconds)
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

            ' Iniciar carregamento do buffer de frames na posição inicial
            CarregarBufferFrames(0)
        End If
    End Sub

    Private Sub CronometroTick(sender As Object, e As EventArgs)
        If mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue Then Return
        If Not isDraggingSlider Then
            Dim pos = mediaClock.CurrentTime.Value.TotalSeconds
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
    Private Sub BtnCapturarInicio_Click(sender As Object, e As RoutedEventArgs)
        If mediaClock IsNot Nothing AndAlso mediaClock.CurrentTime.HasValue Then
            txtInicio.Text = Math.Round(mediaClock.CurrentTime.Value.TotalSeconds, 2).ToString()
        End If
    End Sub

    Private Sub BtnCapturarFim_Click(sender As Object, e As RoutedEventArgs)
        If mediaClock IsNot Nothing AndAlso mediaClock.CurrentTime.HasValue Then
            txtFim.Text = Math.Round(mediaClock.CurrentTime.Value.TotalSeconds, 2).ToString()
        End If
    End Sub

    Private Sub AtualizarIndicadorDeCorte()
        If videoAtual Is Nothing OrElse slLinhaTempo.Maximum <= 0 Then
            bdrCorte.Visibility = Visibility.Hidden
            Return
        End If

        Dim inicio As Double = 0
        Dim fim As Double = 0

        Double.TryParse(txtInicio.Text, inicio)
        Double.TryParse(txtFim.Text, fim)

        If inicio <= 0 AndAlso fim <= 0 Then
            bdrCorte.Visibility = Visibility.Hidden
            Return
        End If

        If fim <= 0 OrElse fim > slLinhaTempo.Maximum Then
            fim = slLinhaTempo.Maximum
        End If

        If inicio < 0 Then inicio = 0
        If inicio > fim Then inicio = fim

        Dim larguraTotal As Double = gridSliderCorte.ActualWidth - 12

        If larguraTotal > 0 Then
            Dim pxInicio = (inicio / slLinhaTempo.Maximum) * larguraTotal
            Dim pxFim = (fim / slLinhaTempo.Maximum) * larguraTotal

            bdrCorte.Width = Math.Max(0, pxFim - pxInicio)
            Canvas.SetLeft(bdrCorte, pxInicio)
            bdrCorte.Visibility = Visibility.Visible
        End If
    End Sub

    Private Sub TxtCorte_TextChanged(sender As Object, e As TextChangedEventArgs)
        If IsLoaded Then
            AtualizarIndicadorDeCorte()
        End If
    End Sub

    Private Sub GridSliderCorte_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        AtualizarIndicadorDeCorte()
    End Sub

    ' --- PROCESSAMENTO EM LOTE (CANCELAMENTO, PREVENÇÃO E UX OTIMIZADOS) ---
    Private Async Sub BtnProcessar_Click(sender As Object, e As RoutedEventArgs)
        If lstGaleria.Items.Count = 0 Then Return

        If estaProcessando Then
            If cts IsNot Nothing Then
                cts.Cancel()
                btnProcessar.Content = "A CANCELAR..."
                btnProcessar.IsEnabled = False
            End If
            Return
        End If

        SalvarConfiguracoesAtuais()
        SalvarExportSettings()

        Dim destino As String = ""

        If rbSalvarPastaEscolhida.IsChecked = True Then
            Using sel As New System.Windows.Forms.FolderBrowserDialog()
                If sel.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
                    destino = sel.SelectedPath
                Else
                    Return
                End If
            End Using
        End If

        estaProcessando = True
        cts = New CancellationTokenSource()

        btnProcessar.Content = "CANCELAR CONVERSÃO (✕)"
        btnProcessar.Background = New SolidColorBrush(Color.FromRgb(200, 50, 50))
        painelProgresso.Visibility = Visibility.Visible

        If mediaClock IsNot Nothing Then
            mediaClock.Controller.Stop()
            VisualizadorVideo.Clock = Nothing
            VisualizadorVideo.Close()
        End If
        AtualizarEstadoPlayPause(False)

        Dim argumentosDesempenho As String = ""
        If rbDesempenhoEco.IsChecked = True Then
            Dim totalThreads = Environment.ProcessorCount
            Dim threadsEconomicas = Math.Max(1, totalThreads \ 2)
            argumentosDesempenho = $"-threads {threadsEconomicas}"
        End If

        Try
            For i = 0 To lstGaleria.Items.Count - 1
                If cts.Token.IsCancellationRequested Then Exit For

                Dim indiceAtual As Integer = i
                Dim tarefa As VideoTarefa = DirectCast(lstGaleria.Items(indiceAtual), VideoTarefa)

                Dim info = FFProbe.Analyse(tarefa.Caminho)

                Dim pastaSaida As String = destino
                If rbSalvarPastaOriginal.IsChecked = True Then
                    pastaSaida = Path.GetDirectoryName(tarefa.Caminho)
                End If

                Dim saida = Path.Combine(pastaSaida, Path.GetFileNameWithoutExtension(tarefa.Caminho) & "_editado.mp4")

                If File.Exists(saida) Then
                    saida = Path.Combine(pastaSaida, Path.GetFileNameWithoutExtension(tarefa.Caminho) & $"_editado_{Date.Now.ToString("HHmmss")}.mp4")
                End If

                Dim bitrateOriginal = info.Format.BitRate / 1000
                Dim bitrateAlvo As Double = If(bitrateOriginal > 6000, 6000, 0)

                Dim duracao As TimeSpan = If(tarefa.Fim > tarefa.Inicio,
                    TimeSpan.FromSeconds(tarefa.Fim - tarefa.Inicio),
                    info.Duration - TimeSpan.FromSeconds(tarefa.Inicio))

                Dim ultimaPorcentagem As Integer = -1

                Dim engine = FFMpegArguments.FromFileInput(tarefa.Caminho).
                    OutputToFile(saida, True, Sub(opt)
                                                  If bitrateAlvo > 0 Then opt.WithVideoBitrate(bitrateAlvo)
                                                  If tarefa.Inicio > 0 Then opt.Seek(TimeSpan.FromSeconds(tarefa.Inicio))
                                                  If tarefa.Fim > tarefa.Inicio Then opt.WithDuration(TimeSpan.FromSeconds(tarefa.Fim - tarefa.Inicio))

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
                                                                        lblPorcentagem.Text = porcentagemAtual.ToString() & "%"
                                                                        lblStatusLote.Text = $"A converter {indiceAtual + 1}/{lstGaleria.Items.Count} - {Path.GetFileName(tarefa.Caminho)}"
                                                                    End Sub)
                                         End If
                                     End Sub, duracao)

                Await engine.CancellableThrough(cts.Token).ProcessAsynchronously()

                If chkDeletarOriginal.IsChecked = True AndAlso Not cts.Token.IsCancellationRequested Then
                    Try
                        File.Delete(tarefa.Caminho)

                        ' Após deletar o original, renomear o ficheiro de saída removendo "_editado"
                        ' para que o nome fique igual ao original, mas mantendo a extensão nova (.mp4)
                        Dim nomeOriginalSemExt = Path.GetFileNameWithoutExtension(tarefa.Caminho)
                        Dim novoNome = nomeOriginalSemExt & ".mp4"
                        Dim caminhoRenomeado = Path.Combine(Path.GetDirectoryName(saida), novoNome)

                        ' Só renomear se o ficheiro de saída realmente contém "_editado" e o nome de destino é diferente
                        If saida <> caminhoRenomeado AndAlso File.Exists(saida) AndAlso Not File.Exists(caminhoRenomeado) Then
                            File.Move(saida, caminhoRenomeado)
                            saida = caminhoRenomeado
                        End If
                    Catch exDel As Exception
                        Debug.WriteLine($"Não foi possível eliminar o original: {exDel.Message}")
                    End Try
                End If
            Next

            If Not cts.Token.IsCancellationRequested Then
                MessageBox.Show("Lote concluído com sucesso!", "Finalizado", MessageBoxButton.OK, MessageBoxImage.Information)
            End If

        Catch ex As OperationCanceledException
            MessageBox.Show("O processo de conversão foi abortado pelo utilizador.", "Cancelado", MessageBoxButton.OK, MessageBoxImage.Warning)
        Catch ex As Exception
            MessageBox.Show("Erro: " & ex.Message, "Falha", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            estaProcessando = False
            If cts IsNot Nothing Then
                cts.Dispose()
                cts = Nothing
            End If

            btnProcessar.IsEnabled = True
            btnProcessar.Content = "CONVERTER TUDO PARA MP4"
            btnProcessar.ClearValue(Button.BackgroundProperty)
            painelProgresso.Visibility = Visibility.Collapsed
            expExportacaoDesempenho.IsExpanded = False
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

    Private Function FormatarTempo(totalSegundos As Double) As String
        If totalSegundos < 0 Then totalSegundos = 0
        Dim ts = TimeSpan.FromSeconds(totalSegundos)
        Dim centesimos = CInt((ts.Milliseconds / 10))
        Dim totalMin = Int(ts.TotalMinutes)
        Return $"{totalMin:00}:{ts.Seconds:00},{centesimos:00}"
    End Function

    Private Sub AvancarFrame(direcao As Integer)
        If mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue Then Return

        If estaReproduzindo Then
            mediaClock.Controller.Pause()
            AtualizarEstadoPlayPause(False)
        End If

        Dim duracaoFrame As Double = 1.0 / frameRate
        Dim novaPosicao As Double = mediaClock.CurrentTime.Value.TotalSeconds + (duracaoFrame * direcao)

        If novaPosicao < 0 Then novaPosicao = 0
        If novaPosicao > slLinhaTempo.Maximum Then novaPosicao = slLinhaTempo.Maximum

        ' Navegar usando sempre o MediaElement (1x)
        mediaClock.Controller.Resume()
        mediaClock.Controller.Seek(TimeSpan.FromSeconds(novaPosicao), TimeSeekOrigin.BeginTime)
        Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                  If Not estaReproduzindo Then
                                                                      mediaClock.Controller.Pause()
                                                                  End If
                                                              End Sub)

        slLinhaTempo.Value = novaPosicao
        lblTempoAtual.Text = FormatarTempo(novaPosicao)
    End Sub

    ' --- MODO CRONOANÁLISE ---

    Private Sub BtnModoCrono_Checked(sender As Object, e As RoutedEventArgs)
        modoCronoAtivo = True
        grpConfigVideo.Visibility = Visibility.Collapsed
        painelExportacao.Visibility = Visibility.Collapsed
        pnlCrono.Visibility = Visibility.Visible
        btnModoCrono.Background = New SolidColorBrush(Color.FromRgb(106, 76, 147))
        btnModoCrono.Foreground = Brushes.White
    End Sub

    Private Sub BtnModoCrono_Unchecked(sender As Object, e As RoutedEventArgs)
        modoCronoAtivo = False

        ' Se o painel estiver desacoplado, fechar a janela flutuante (isso reacopla automaticamente)
        If cronoPanelWindow IsNot Nothing Then
            cronoPanelWindow.Close()
        End If

        pnlCrono.Visibility = Visibility.Collapsed
        grpConfigVideo.Visibility = Visibility.Visible
        painelExportacao.Visibility = Visibility.Visible
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
            MessageBox.Show("Abra um vídeo antes de adicionar registos de cronoanálise.", "Cronoanálise", MessageBoxButton.OK, MessageBoxImage.Warning)
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
            MessageBox.Show("Não há registos para exportar.", "Cronoanálise", MessageBoxButton.OK, MessageBoxImage.Information)
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
            If MessageBox.Show("Limpar todos os registos da cronoanálise?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question) = MessageBoxResult.Yes Then
                cronoEntries.Clear()
            End If
        End If
    End Sub

    Private Sub BtnLimparCache_Click(sender As Object, e As RoutedEventArgs)
        Dim resultado = MessageBox.Show(
            "Limpar todo o cache de cronoanálise?" & vbCrLf &
            "Isso irá apagar todos os dados salvos de todos os vídeos." & vbCrLf &
            "Os registos atuais na grelha também serão limpos.",
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
                MessageBox.Show("Erro ao apagar ficheiro de cache: " & ex.Message)
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

    Private Sub GridCrono_SelectedCellsChanged(sender As Object, e As SelectedCellsChangedEventArgs)
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

    Private Sub GridCrono_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs)
        gridCrono.Focus()

        ' Atualizar análise estatística após edição
        Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                  AtualizarAnaliseEstatistica()
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
                End If

                Return False
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

    ' --- CONFIGURAÇÕES DE EXPORTAÇÃO (SALVAS EM JSON NO APPDATA) ---

    Private Sub CarregarExportSettings()
        Dim settings = ExportSettingsManager.Carregar()

        ' Aplicar configurações de local de guardar
        rbSalvarPastaOriginal.IsChecked = settings.SalvarNaPastaOriginal
        rbSalvarPastaEscolhida.IsChecked = Not settings.SalvarNaPastaOriginal

        ' Aplicar configuração de deletar original
        chkDeletarOriginal.IsChecked = settings.DeletarOriginal

        ' Aplicar modo de desempenho
        rbDesempenhoEco.IsChecked = settings.ModoEconomico
        rbDesempenhoAlto.IsChecked = Not settings.ModoEconomico

        ' Atualizar estado do checkbox conforme a opção de pasta selecionada
        AtualizarEstadoCheckboxDeletar()
    End Sub

    Private Sub SalvarExportSettings()
        Dim settings As New ExportSettings() With {
            .SalvarNaPastaOriginal = rbSalvarPastaOriginal.IsChecked.HasValue AndAlso rbSalvarPastaOriginal.IsChecked.Value,
            .DeletarOriginal = chkDeletarOriginal.IsChecked.HasValue AndAlso chkDeletarOriginal.IsChecked.Value,
            .ModoEconomico = rbDesempenhoEco.IsChecked.HasValue AndAlso rbDesempenhoEco.IsChecked.Value
        }
        ExportSettingsManager.Salvar(settings)
    End Sub

    Private Sub AtualizarEstadoCheckboxDeletar()
        ' Desabilitar o checkbox "Eliminar original" quando a opção "Escolher uma pasta" estiver ativa,
        ' pois o utilizador está a guardar noutra pasta e não faz sentido eliminar o original.
        ' Habilitar quando "Guardar na mesma pasta do original" estiver selecionado.
        Dim salvarNaOriginal As Boolean = rbSalvarPastaOriginal.IsChecked.HasValue AndAlso rbSalvarPastaOriginal.IsChecked.Value
        chkDeletarOriginal.IsEnabled = salvarNaOriginal

        ' Se estiver desabilitado e estiver marcado, desmarcar automaticamente
        If Not chkDeletarOriginal.IsEnabled Then
            chkDeletarOriginal.IsChecked = False
        End If
    End Sub

    Private Sub RbSalvarPasta_Checked(sender As Object, e As RoutedEventArgs)
        If _isLoading Then Return
        AtualizarEstadoCheckboxDeletar()
        SalvarExportSettings()
    End Sub

    Private Sub ChkDeletarOriginal_Checked(sender As Object, e As RoutedEventArgs)
        If _isLoading Then Return
        SalvarExportSettings()
    End Sub

    Private Sub RbDesempenho_Checked(sender As Object, e As RoutedEventArgs)
        If _isLoading Then Return
        SalvarExportSettings()
    End Sub
End Class