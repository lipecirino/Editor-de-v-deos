Imports System.Windows.Media

''' <summary>Tipo do aviso: define o ícone e a cor.</summary>
Public Enum TipoAviso
    Informacao
    Sucesso
    Atencao
    Erro
    Pergunta
End Enum

''' <summary>
''' Avisos e perguntas no estilo do app (substitui o MessageBox padrão do Windows).
''' Em perguntas, o botão padrão (Enter/Esc) é sempre o de NÃO confirmar, para evitar ações acidentais.
''' </summary>
Partial Public Class ConfirmacaoWindow

    Private Sub New(titulo As String, mensagem As String, tipo As TipoAviso,
                    textoConfirmar As String, textoCancelar As String, destrutivo As Boolean)
        InitializeComponent()
        lblTitulo.Text = titulo
        lblMensagem.Text = mensagem
        lblMensagem.Visibility = If(String.IsNullOrWhiteSpace(mensagem), Visibility.Collapsed, Visibility.Visible)
        Title = titulo

        ' Ícone e cor conforme o tipo
        Dim glifo As Integer, cor As Color, fundo As Color
        Select Case tipo
            Case TipoAviso.Sucesso
                glifo = &HE73E : cor = Color.FromRgb(&H34, &HC7, &H59) : fundo = Color.FromRgb(&HE3, &HF6, &HE8)
            Case TipoAviso.Atencao
                glifo = &HE7BA : cor = Color.FromRgb(&HFF, &H95, &H0) : fundo = Color.FromRgb(&HFF, &HF4, &HE5)
            Case TipoAviso.Erro
                glifo = &HEA39 : cor = Color.FromRgb(&HFF, &H3B, &H30) : fundo = Color.FromRgb(&HFF, &HEB, &HEA)
            Case TipoAviso.Pergunta
                glifo = &HE9CE : cor = Color.FromRgb(&H0, &H7A, &HFF) : fundo = Color.FromRgb(&HE5, &HF1, &HFF)
            Case Else
                glifo = &HE946 : cor = Color.FromRgb(&H0, &H7A, &HFF) : fundo = Color.FromRgb(&HE5, &HF1, &HFF)
        End Select
        If destrutivo Then
            glifo = &HE7BA : cor = Color.FromRgb(&HFF, &H3B, &H30) : fundo = Color.FromRgb(&HFF, &HEB, &HEA)
        End If
        icoConfirmacao.Text = ChrW(glifo).ToString()
        icoConfirmacao.Foreground = New SolidColorBrush(cor)
        bdrIcone.Background = New SolidColorBrush(fundo)

        ' Botões: aviso tem só "OK"; pergunta tem confirmar + cancelar
        btnSim.Content = textoConfirmar
        If destrutivo Then btnSim.Background = New SolidColorBrush(Color.FromRgb(&HFF, &H3B, &H30))
        If String.IsNullOrEmpty(textoCancelar) Then
            btnNao.Visibility = Visibility.Collapsed
            btnNao.IsDefault = False
            btnNao.IsCancel = False
            btnSim.IsDefault = True
            btnSim.IsCancel = True
        Else
            btnNao.Content = textoCancelar
        End If
    End Sub

    ' Usa a janela ativa do app como dona (centraliza sobre ela); sem janela visível, centraliza na tela
    Private Shared Function Mostrar(janela As ConfirmacaoWindow, dono As Window) As Boolean
        If dono Is Nothing AndAlso Application.Current IsNot Nothing Then
            dono = Application.Current.Windows.OfType(Of Window)().FirstOrDefault(Function(w) w.IsActive)
            If dono Is Nothing Then dono = Application.Current.MainWindow
        End If
        If dono IsNot Nothing AndAlso dono.IsLoaded AndAlso dono.IsVisible AndAlso Not Object.ReferenceEquals(dono, janela) Then
            janela.Owner = dono
        Else
            janela.WindowStartupLocation = WindowStartupLocation.CenterScreen
        End If
        Return janela.ShowDialog() = True
    End Function

    ''' <summary>Pergunta sim/não. Devolve True se o usuário confirmou.</summary>
    Public Shared Function Perguntar(dono As Window, titulo As String, mensagem As String, textoConfirmar As String,
                                     textoCancelar As String, Optional destrutivo As Boolean = True) As Boolean
        Return Mostrar(New ConfirmacaoWindow(titulo, mensagem, TipoAviso.Pergunta, textoConfirmar, textoCancelar, destrutivo), dono)
    End Function

    ''' <summary>Aviso com um único botão "OK".</summary>
    Public Shared Sub Avisar(dono As Window, titulo As String, mensagem As String, Optional tipo As TipoAviso = TipoAviso.Informacao)
        Mostrar(New ConfirmacaoWindow(titulo, mensagem, tipo, "OK", Nothing, False), dono)
    End Sub

    Private Sub BtnSim_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = True
    End Sub

    Private Sub BtnNao_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub
End Class
