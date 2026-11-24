Imports System.Windows.Media

''' <summary>
''' Pergunta de confirmação no estilo do app (substitui o MessageBox padrão do Windows).
''' O botão padrão (Enter/Esc) é sempre o de NÃO confirmar, para evitar ações acidentais.
''' </summary>
Partial Public Class ConfirmacaoWindow

    Public Sub New(titulo As String, mensagem As String, textoConfirmar As String, textoCancelar As String,
                   Optional destrutivo As Boolean = True)
        InitializeComponent()
        lblTitulo.Text = titulo
        lblMensagem.Text = mensagem
        btnSim.Content = textoConfirmar
        btnNao.Content = textoCancelar

        If destrutivo Then
            btnSim.Background = New SolidColorBrush(Color.FromRgb(&HFF, &H3B, &H30))
        Else
            bdrIcone.Background = New SolidColorBrush(Color.FromRgb(&HE5, &HF1, &HFF))
            icoConfirmacao.Foreground = New SolidColorBrush(Color.FromRgb(&H0, &H7A, &HFF))
            icoConfirmacao.Text = ChrW(&HE946).ToString()
        End If
    End Sub

    ''' <summary>Mostra a pergunta e devolve True se o usuário confirmou.</summary>
    Public Shared Function Perguntar(dono As Window, titulo As String, mensagem As String, textoConfirmar As String,
                                     textoCancelar As String, Optional destrutivo As Boolean = True) As Boolean
        Dim janela As New ConfirmacaoWindow(titulo, mensagem, textoConfirmar, textoCancelar, destrutivo) With {.Owner = dono}
        Return janela.ShowDialog() = True
    End Function

    Private Sub BtnSim_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = True
    End Sub

    Private Sub BtnNao_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub
End Class
