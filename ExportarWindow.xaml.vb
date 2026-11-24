Imports System.IO
Imports System.Windows.Media

''' <summary>
''' Tela de exportação: como exportar os segmentos, onde salvar, exclusão dos originais e desempenho.
''' As escolhas ficam salvas em ExportSettings para a próxima vez.
''' </summary>
Partial Public Class ExportarWindow
    Private ReadOnly _settings As ExportSettings
    Private ReadOnly _qtdVideos As Integer

    ''' <summary>True = juntar os segmentos em um vídeo só; False = um vídeo por segmento.</summary>
    Public Property ExportarUnico As Boolean

    ''' <summary>Pasta escolhida quando a opção é "Outra pasta" (vazio = mesma pasta do original).</summary>
    Public Property PastaDestino As String = ""

    Public Sub New(settings As ExportSettings, qtdVideos As Integer, ignoradosCrono As Integer, temSegmentos As Boolean)
        InitializeComponent()
        _settings = settings
        _qtdVideos = qtdVideos

        Dim resumo = If(qtdVideos = 1, "1 vídeo será convertido para MP4.", $"{qtdVideos} vídeos serão convertidos para MP4.")
        If ignoradosCrono > 0 Then
            resumo &= If(ignoradosCrono = 1, " 1 vídeo com cronoanálise fica de fora.", $" {ignoradosCrono} vídeos com cronoanálise ficam de fora.")
        End If
        lblResumo.Text = resumo

        pnlSegmentos.Visibility = If(temSegmentos, Visibility.Visible, Visibility.Collapsed)

        ' Preencher com as últimas escolhas
        PastaDestino = If(settings.PastaDestino, "")
        If Not String.IsNullOrEmpty(PastaDestino) Then lblPastaDestino.Text = PastaDestino
        rbPastaOriginal.IsChecked = settings.SalvarNaPastaOriginal
        rbOutraPasta.IsChecked = Not settings.SalvarNaPastaOriginal
        chkExcluirOriginal.IsChecked = settings.DeletarOriginal
        rbEconomico.IsChecked = settings.ModoEconomico
        rbAlto.IsChecked = Not settings.ModoEconomico
        rbUnico.IsChecked = settings.JuntarSegmentos
        rbSeparados.IsChecked = Not settings.JuntarSegmentos

        AtualizarEstado()
    End Sub

    Private Sub Destino_Checked(sender As Object, e As RoutedEventArgs)
        AtualizarEstado()
    End Sub

    Private Sub Excluir_Changed(sender As Object, e As RoutedEventArgs)
        AtualizarEstado()
    End Sub

    ' Excluir originais só vale salvando na mesma pasta; o botão principal avisa quando vai excluir
    Private Sub AtualizarEstado()
        If chkExcluirOriginal Is Nothing OrElse btnExportar Is Nothing Then Return

        Dim mesmaPasta = (rbPastaOriginal.IsChecked = True)
        chkExcluirOriginal.IsEnabled = mesmaPasta
        bdrExcluir.Opacity = If(mesmaPasta, 1.0, 0.5)
        If Not mesmaPasta Then chkExcluirOriginal.IsChecked = False

        Dim excluir = mesmaPasta AndAlso chkExcluirOriginal.IsChecked = True
        If excluir Then
            bdrExcluir.Background = New SolidColorBrush(Color.FromRgb(&HFF, &HEB, &HEA))
            bdrExcluir.BorderBrush = New SolidColorBrush(Color.FromRgb(&HFF, &H3B, &H30))
            lblExcluirDica.Text = If(_qtdVideos = 1, "O vídeo original será apagado. Não dá para desfazer.",
                                     $"Os {_qtdVideos} vídeos originais serão apagados. Não dá para desfazer.")
            btnExportar.Content = "Exportar e excluir originais"
            btnExportar.Background = New SolidColorBrush(Color.FromRgb(&HFF, &H3B, &H30))
        Else
            bdrExcluir.Background = Brushes.White
            bdrExcluir.BorderBrush = New SolidColorBrush(Color.FromRgb(&HE5, &HE5, &HEA))
            lblExcluirDica.Text = "Só disponível ao salvar na mesma pasta do original. Não dá para desfazer."
            btnExportar.Content = "Exportar"
            btnExportar.ClearValue(Button.BackgroundProperty)
        End If
    End Sub

    Private Sub BtnEscolherPasta_Click(sender As Object, e As RoutedEventArgs)
        If EscolherPasta() Then rbOutraPasta.IsChecked = True
    End Sub

    Private Function EscolherPasta() As Boolean
        Using sel As New System.Windows.Forms.FolderBrowserDialog()
            If Not String.IsNullOrEmpty(PastaDestino) AndAlso Directory.Exists(PastaDestino) Then sel.SelectedPath = PastaDestino
            If sel.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
                PastaDestino = sel.SelectedPath
                lblPastaDestino.Text = PastaDestino
                Return True
            End If
        End Using
        Return False
    End Function

    Private Sub BtnExportar_Click(sender As Object, e As RoutedEventArgs)
        ' "Outra pasta" sem pasta válida: pede a pasta antes de seguir
        If rbOutraPasta.IsChecked = True AndAlso (String.IsNullOrEmpty(PastaDestino) OrElse Not Directory.Exists(PastaDestino)) Then
            If Not EscolherPasta() Then Return
        End If

        ExportarUnico = (rbUnico.IsChecked = True)

        _settings.SalvarNaPastaOriginal = (rbPastaOriginal.IsChecked = True)
        _settings.DeletarOriginal = _settings.SalvarNaPastaOriginal AndAlso chkExcluirOriginal.IsChecked = True
        _settings.ModoEconomico = (rbEconomico.IsChecked = True)
        _settings.PastaDestino = PastaDestino
        _settings.JuntarSegmentos = ExportarUnico
        ExportSettingsManager.Salvar(_settings)

        DialogResult = True
    End Sub

    Private Sub BtnCancelar_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
    End Sub
End Class
