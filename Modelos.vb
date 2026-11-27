Imports System.ComponentModel
Imports System.IO
Imports System.Windows.Media

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

    ' Índices dos campos (4 pares início/fim)
    Public Const IDX_INICIO1 As Integer = 0
    Public Const IDX_FIM1 As Integer = 1
    Public Const IDX_INICIO2 As Integer = 2
    Public Const IDX_FIM2 As Integer = 3
    Public Const IDX_INICIO3 As Integer = 4
    Public Const IDX_FIM3 As Integer = 5
    Public Const IDX_INICIO4 As Integer = 6
    Public Const IDX_FIM4 As Integer = 7
    Public Const NUM_CAMPOS As Integer = 8
    Public Const NUM_PARES As Integer = NUM_CAMPOS \ 2

    ' Nome da propriedade de cada campo (o Display é o mesmo nome + "Display")
    Private Shared ReadOnly NomesCampos As String() = {"Inicio1", "Fim1", "Inicio2", "Fim2", "Inicio3", "Fim3", "Inicio4", "Fim4"}

    ' Grava o valor (segundos) do campo, atualiza o texto exibido e avisa a tela
    Private Sub DefinirValor(indice As Integer, valor As Double)
        _valores(indice) = valor
        DefinirDisplay(indice, SegundosParaCrono(valor))
        RaisePropertyChanged(NomesCampos(indice))
        RaisePropertyChanged(NameOf(DuracaoDisplay))
    End Sub

    ' Grava o texto (mm:ss,cc) do campo; se for um tempo válido, atualiza também o valor
    Private Sub DefinirDisplay(indice As Integer, texto As String)
        If texto = _displays(indice) Then Return
        _displays(indice) = texto
        Dim parsed = CronoParaSegundos(texto)
        If parsed.HasValue Then
            _valores(indice) = parsed.Value
            RaisePropertyChanged(NomesCampos(indice))
            RaisePropertyChanged(NameOf(DuracaoDisplay))
        End If
        RaisePropertyChanged(NomesCampos(indice) & "Display")
    End Sub

    ' Propriedades de valor (segundos)
    Public Property Inicio1 As Double
        Get
            Return _valores(IDX_INICIO1)
        End Get
        Set(value As Double)
            DefinirValor(IDX_INICIO1, value)
        End Set
    End Property

    Public Property Fim1 As Double
        Get
            Return _valores(IDX_FIM1)
        End Get
        Set(value As Double)
            DefinirValor(IDX_FIM1, value)
        End Set
    End Property

    Public Property Inicio2 As Double
        Get
            Return _valores(IDX_INICIO2)
        End Get
        Set(value As Double)
            DefinirValor(IDX_INICIO2, value)
        End Set
    End Property

    Public Property Fim2 As Double
        Get
            Return _valores(IDX_FIM2)
        End Get
        Set(value As Double)
            DefinirValor(IDX_FIM2, value)
        End Set
    End Property

    Public Property Inicio3 As Double
        Get
            Return _valores(IDX_INICIO3)
        End Get
        Set(value As Double)
            DefinirValor(IDX_INICIO3, value)
        End Set
    End Property

    Public Property Fim3 As Double
        Get
            Return _valores(IDX_FIM3)
        End Get
        Set(value As Double)
            DefinirValor(IDX_FIM3, value)
        End Set
    End Property

    Public Property Inicio4 As Double
        Get
            Return _valores(IDX_INICIO4)
        End Get
        Set(value As Double)
            DefinirValor(IDX_INICIO4, value)
        End Set
    End Property

    Public Property Fim4 As Double
        Get
            Return _valores(IDX_FIM4)
        End Get
        Set(value As Double)
            DefinirValor(IDX_FIM4, value)
        End Set
    End Property

    ' Propriedades de display (mm:ss,cc), ligadas às células editáveis do grid
    Public Property Inicio1Display As String
        Get
            Return _displays(IDX_INICIO1)
        End Get
        Set(value As String)
            DefinirDisplay(IDX_INICIO1, value)
        End Set
    End Property

    Public Property Fim1Display As String
        Get
            Return _displays(IDX_FIM1)
        End Get
        Set(value As String)
            DefinirDisplay(IDX_FIM1, value)
        End Set
    End Property

    Public Property Inicio2Display As String
        Get
            Return _displays(IDX_INICIO2)
        End Get
        Set(value As String)
            DefinirDisplay(IDX_INICIO2, value)
        End Set
    End Property

    Public Property Fim2Display As String
        Get
            Return _displays(IDX_FIM2)
        End Get
        Set(value As String)
            DefinirDisplay(IDX_FIM2, value)
        End Set
    End Property

    Public Property Inicio3Display As String
        Get
            Return _displays(IDX_INICIO3)
        End Get
        Set(value As String)
            DefinirDisplay(IDX_INICIO3, value)
        End Set
    End Property

    Public Property Fim3Display As String
        Get
            Return _displays(IDX_FIM3)
        End Get
        Set(value As String)
            DefinirDisplay(IDX_FIM3, value)
        End Set
    End Property

    Public Property Inicio4Display As String
        Get
            Return _displays(IDX_INICIO4)
        End Get
        Set(value As String)
            DefinirDisplay(IDX_INICIO4, value)
        End Set
    End Property

    Public Property Fim4Display As String
        Get
            Return _displays(IDX_FIM4)
        End Get
        Set(value As String)
            DefinirDisplay(IDX_FIM4, value)
        End Set
    End Property

    ' Retorna o índice do próximo campo vazio (0-7) ou -1 se todos cheios
    Public Function ObterProximoCampoVazio() As Integer
        For i As Integer = 0 To NUM_CAMPOS - 1
            If _valores(i) = 0 Then
                Return i
            End If
        Next
        Return -1
    End Function

    ' Valor (segundos) e texto exibido do campo pelo índice (0-7)
    Public Function ValorCampo(indice As Integer) As Double
        Return _valores(indice)
    End Function

    Public Function DisplayCampo(indice As Integer) As String
        Return _displays(indice)
    End Function

    ' Define o valor no índice especificado
    Public Sub SetValorCampo(indice As Integer, valor As Double)
        If indice < 0 OrElse indice >= NUM_CAMPOS Then Return
        DefinirValor(indice, valor)
    End Sub

    ' Trechos válidos (fim depois do início) desta linha, ordenados pelo início, como segmentos da timeline
    Public Function ObterTrechos() As List(Of SegmentoCorte)
        Dim trechos As New List(Of SegmentoCorte)()
        For p = 0 To NUM_PARES - 1
            Dim ini = _valores(2 * p), fim = _valores(2 * p + 1)
            If fim > ini AndAlso fim > 0 Then
                trechos.Add(New SegmentoCorte() With {.Inicio = ini, .Fim = fim, .Rotulo = If(Operacao, ""), .Dono = Me})
            End If
        Next
        trechos.Sort(Function(x, y) x.Inicio.CompareTo(y.Inicio))
        Return trechos
    End Function

    ' --- Conversão de/para o cache em disco ---
    Public Shared Function DoCache(c As CronoAnaliseCacheEntry) As CronoAnaliseEntry
        Return New CronoAnaliseEntry() With {
            .Operacao = c.Operacao, .Cliente = c.Cliente, .NumeroAmostras = c.NumeroAmostras,
            .Inicio1 = c.Inicio1, .Fim1 = c.Fim1, .Inicio2 = c.Inicio2, .Fim2 = c.Fim2,
            .Inicio3 = c.Inicio3, .Fim3 = c.Fim3, .Inicio4 = c.Inicio4, .Fim4 = c.Fim4}
    End Function

    Public Function ParaCache() As CronoAnaliseCacheEntry
        Return New CronoAnaliseCacheEntry() With {
            .Operacao = Operacao, .Cliente = Cliente, .NumeroAmostras = NumeroAmostras,
            .Inicio1 = Inicio1, .Fim1 = Fim1, .Inicio2 = Inicio2, .Fim2 = Fim2,
            .Inicio3 = Inicio3, .Fim3 = Fim3, .Inicio4 = Inicio4, .Fim4 = Fim4}
    End Function

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

    ' Duração total calculada: soma dos pares (Fim - Início), formatada como segundos,centésimos (ex: 125,67)
    Public ReadOnly Property DuracaoDisplay As String
        Get
            Dim total = DuracaoSegundos
            If total <= 0 Then Return ""
            Dim segundosInt = CInt(Math.Floor(total))
            Dim centesimos = CInt((total - segundosInt) * 100)
            Return $"{segundosInt},{centesimos:D2}"
        End Get
    End Property

    ' Duração em segundos (para cálculos)
    Public ReadOnly Property DuracaoSegundos As Double
        Get
            Dim total As Double = 0
            For p = 0 To NUM_PARES - 1
                total += Math.Max(0, _valores(2 * p + 1) - _valores(2 * p))
            Next
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

' Linha da visão geral "Todos os vídeos": uma operação de um vídeo (somente leitura)
Public Class CronoGeralLinha
    Public Property CaminhoVideo As String = ""
    Public Property NomeVideo As String = ""
    Public Property Entry As CronoAnaliseEntry

    Public ReadOnly Property Operacao As String
        Get
            Return Entry.Operacao
        End Get
    End Property

    Public ReadOnly Property Cliente As String
        Get
            Return Entry.Cliente
        End Get
    End Property

    Public ReadOnly Property NumeroAmostras As Integer
        Get
            Return Entry.NumeroAmostras
        End Get
    End Property

    Public ReadOnly Property DuracaoTexto As String
        Get
            Return Entry.DuracaoDisplay
        End Get
    End Property
End Class

' Um vídeo na visão geral: cabeçalho (nome, caminho) e suas operações
Public Class CronoGeralVideo
    Public Property CaminhoVideo As String = ""
    Public Property NomeVideo As String = ""
    Public Property Linhas As New List(Of CronoGeralLinha)()

    Public ReadOnly Property Resumo As String
        Get
            Return $" · {Linhas.Count} {If(Linhas.Count = 1, "operação", "operações")}"
        End Get
    End Property
End Class
