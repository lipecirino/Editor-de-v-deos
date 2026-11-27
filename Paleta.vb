Imports System.Windows.Media

' Cores do padrão visual do programa. A fonte única é o Application.xaml (pincéis "Cor..."),
' usados direto no XAML com {StaticResource}; aqui ficam os mesmos pincéis para o código.
Public Module Paleta
    Private Function Recurso(chave As String) As SolidColorBrush
        Return DirectCast(System.Windows.Application.Current.FindResource(chave), SolidColorBrush)
    End Function

    Public ReadOnly Property Azul As SolidColorBrush
        Get
            Return Recurso("CorAzul")
        End Get
    End Property

    Public ReadOnly Property AzulClaro As SolidColorBrush
        Get
            Return Recurso("CorAzulClaro")
        End Get
    End Property

    Public ReadOnly Property Verde As SolidColorBrush
        Get
            Return Recurso("CorVerde")
        End Get
    End Property

    Public ReadOnly Property VerdeClaro As SolidColorBrush
        Get
            Return Recurso("CorVerdeClaro")
        End Get
    End Property

    Public ReadOnly Property Laranja As SolidColorBrush
        Get
            Return Recurso("CorLaranja")
        End Get
    End Property

    Public ReadOnly Property LaranjaClaro As SolidColorBrush
        Get
            Return Recurso("CorLaranjaClaro")
        End Get
    End Property

    Public ReadOnly Property Vermelho As SolidColorBrush
        Get
            Return Recurso("CorVermelho")
        End Get
    End Property

    Public ReadOnly Property VermelhoClaro As SolidColorBrush
        Get
            Return Recurso("CorVermelhoClaro")
        End Get
    End Property

    Public ReadOnly Property Texto As SolidColorBrush
        Get
            Return Recurso("CorTexto")
        End Get
    End Property

    Public ReadOnly Property TextoSecundario As SolidColorBrush
        Get
            Return Recurso("CorTextoSecundario")
        End Get
    End Property

    Public ReadOnly Property Cinza As SolidColorBrush
        Get
            Return Recurso("CorCinza")
        End Get
    End Property

    Public ReadOnly Property CinzaDesativado As SolidColorBrush
        Get
            Return Recurso("CorCinzaDesativado")
        End Get
    End Property

    Public ReadOnly Property Borda As SolidColorBrush
        Get
            Return Recurso("CorBorda")
        End Get
    End Property

    Public ReadOnly Property BordaForte As SolidColorBrush
        Get
            Return Recurso("CorBordaForte")
        End Get
    End Property

    Public ReadOnly Property BotaoFundo As SolidColorBrush
        Get
            Return Recurso("CorBotaoFundo")
        End Get
    End Property

    Public ReadOnly Property FundoCinza As SolidColorBrush
        Get
            Return Recurso("CorFundoCinza")
        End Get
    End Property

    ' Pincel congelado (mais leve) para cores avulsas criadas em código
    Public Function Pincel(cor As Color) As SolidColorBrush
        Dim p As New SolidColorBrush(cor)
        p.Freeze()
        Return p
    End Function

    ' Mesma cor com outra transparência (0 = invisível, 255 = opaca)
    Public Function ComAlfa(base As SolidColorBrush, alfa As Byte) As SolidColorBrush
        Dim c = base.Color
        Return Paleta.Pincel(Color.FromArgb(alfa, c.R, c.G, c.B))
    End Function
End Module

' Faixas de regularidade usadas na cronoanálise (desvio da média e coeficiente de variação):
' verde até 5%, amarelo até 15%, laranja até 30%, vermelho acima
Public Class FaixaDesvio
    Public ReadOnly Property Cor As SolidColorBrush         ' barras e pontos
    Public ReadOnly Property Fundo As SolidColorBrush       ' fundo de cartão
    Public ReadOnly Property TextoCor As SolidColorBrush    ' texto sobre o fundo

    Private Sub New(cor As Color, fundo As Color, texto As Color)
        Me.Cor = Paleta.Pincel(cor)
        Me.Fundo = Paleta.Pincel(fundo)
        Me.TextoCor = Paleta.Pincel(texto)
    End Sub

    Private Shared ReadOnly Faixas As FaixaDesvio() = {
        New FaixaDesvio(Color.FromRgb(&H4C, &HAF, &H50), Color.FromRgb(&HE8, &HF6, &HE9), Color.FromRgb(&H2E, &H7D, &H32)),
        New FaixaDesvio(Color.FromRgb(&HFF, &HC1, &H7), Color.FromRgb(&HFF, &HF6, &HD9), Color.FromRgb(&H9A, &H72, &H0)),
        New FaixaDesvio(Color.FromRgb(&HFF, &H98, &H0), Color.FromRgb(&HFF, &HEC, &HD6), Color.FromRgb(&HC2, &H6A, &H0)),
        New FaixaDesvio(Color.FromRgb(&HF4, &H43, &H36), Color.FromRgb(&HFD, &HE8, &HE6), Color.FromRgb(&HC6, &H28, &H28))}

    ' Faixa do percentual (o sinal é ignorado)
    Public Shared Function De(percentual As Double) As FaixaDesvio
        Dim d = Math.Abs(percentual)
        If d <= 5 Then Return Faixas(0)
        If d <= 15 Then Return Faixas(1)
        If d <= 30 Then Return Faixas(2)
        Return Faixas(3)
    End Function
End Class

' Estatísticas de uma lista de tempos (desvio padrão populacional, como no restante do programa)
Public Class EstatisticaTempos
    Public ReadOnly Property N As Integer
    Public ReadOnly Property Media As Double
    Public ReadOnly Property DesvioPadrao As Double
    Public ReadOnly Property Minimo As Double
    Public ReadOnly Property Maximo As Double

    Public Sub New(tempos As IEnumerable(Of Double))
        Dim lista = tempos.ToList()
        N = lista.Count
        If N = 0 Then Return
        Media = lista.Average()
        Dim m = Media
        DesvioPadrao = If(N > 1, Math.Sqrt(lista.Sum(Function(t) (t - m) * (t - m)) / N), 0)
        Minimo = lista.Min()
        Maximo = lista.Max()
    End Sub

    ' Coeficiente de variação, em %
    Public ReadOnly Property Cv As Double
        Get
            Return If(Media > 0, DesvioPadrao / Media * 100.0, 0)
        End Get
    End Property

    ' Desvio de um tempo em relação à média, em %
    Public Function DesvioPercentual(t As Double) As Double
        Return If(Media > 0, (t - Media) / Media * 100.0, 0)
    End Function

    ' Chave para agrupar operações pelo nome (ignora maiúsculas e espaços nas pontas)
    Public Shared Function ChaveOperacao(nome As String) As String
        Return If(nome, "").Trim().ToUpperInvariant()
    End Function
End Class
