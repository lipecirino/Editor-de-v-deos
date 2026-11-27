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
        Dim glifo As Integer, cor As Brush, fundo As Brush
        Select Case tipo
            Case TipoAviso.Sucesso
                glifo = &HE73E : cor = Paleta.Verde : fundo = Paleta.VerdeClaro
            Case TipoAviso.Atencao
                glifo = &HE7BA : cor = Paleta.Laranja : fundo = Paleta.LaranjaClaro
            Case TipoAviso.Erro
                glifo = &HEA39 : cor = Paleta.Vermelho : fundo = Paleta.VermelhoClaro
            Case TipoAviso.Pergunta
                glifo = &HE9CE : cor = Paleta.Azul : fundo = Paleta.AzulClaro
            Case Else
                glifo = &HE946 : cor = Paleta.Azul : fundo = Paleta.AzulClaro
        End Select
        If destrutivo Then
            glifo = &HE7BA : cor = Paleta.Vermelho : fundo = Paleta.VermelhoClaro
        End If
        icoConfirmacao.Text = ChrW(glifo).ToString()
        icoConfirmacao.Foreground = cor
        bdrIcone.Background = fundo

        ' Botões: aviso tem só "OK"; pergunta tem confirmar + cancelar
        btnSim.Content = textoConfirmar
        If destrutivo Then btnSim.Background = Paleta.Vermelho
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
