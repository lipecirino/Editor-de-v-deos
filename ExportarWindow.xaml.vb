Imports System.IO
Imports System.Windows.Media

''' <summary>
''' Tela de exportação: como exportar os segmentos, onde salvar, exclusão dos originais e desempenho.
''' As escolhas ficam salvas em ExportSettings para a próxima vez.
''' </summary>
Partial Public Class ExportarWindow
    Private ReadOnly _settings As ExportSettings
    Private ReadOnly _qtdVideos As Integer
    Private ReadOnly _temSegmentos As Boolean
    Private _pronto As Boolean = False   ' ignora eventos disparados durante o InitializeComponent

    ''' <summary>True = juntar os segmentos em um vídeo só; False = um vídeo por segmento.</summary>
    Public Property ExportarUnico As Boolean

    ''' <summary>Pasta escolhida quando a opção é "Outra pasta" (vazio = mesma pasta do original).</summary>
    Public Property PastaDestino As String = ""

    ''' <summary>Nome da subpasta padronizada a criar dentro de PastaDestino (vazio = não criar).</summary>
    Public Property NomePastaPadronizada As String = ""

    Private Shared Function LimparNome(s As String) As String
        Dim r = If(s, "").Trim()
        For Each c In Path.GetInvalidFileNameChars()
            r = r.Replace(c, " "c)
        Next
        Return r.Trim().TrimEnd("."c)
    End Function

    Private Shared ReadOnly UFsValidas As New HashSet(Of String) From {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR",
        "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"}

    ''' <summary>Monta "(Cliente - Cidade - UF) - (Velocidade) - (aaaa.mm.dd)"; vazio se faltar algum campo.</summary>
    Private Function MontarNomePasta() As String
        If CamposFaltando().Count > 0 Then Return ""
        Dim cliente = LimparNome(txtCliente.Text)
        Dim cidade = LimparNome(txtCidade.Text)
        Dim uf = LimparNome(txtEstado.Text)
        Dim vel = LimparNome(txtVelocidade.Text)
        Return $"({cliente} - {cidade} - {uf}) - ({vel}) - ({dpData.SelectedDate.Value:yyyy.MM.dd})"
    End Function

    ' Campos da pasta padronizada ainda vazios ou inválidos (rótulo para a mensagem e o controle para destacar)
    Private Function CamposFaltando() As List(Of KeyValuePair(Of String, Control))
        Dim faltando As New List(Of KeyValuePair(Of String, Control))
        If LimparNome(txtCliente.Text) = "" Then faltando.Add(New KeyValuePair(Of String, Control)("cliente", txtCliente))
        If LimparNome(txtCidade.Text) = "" Then faltando.Add(New KeyValuePair(Of String, Control)("cidade", txtCidade))
        Dim uf = LimparNome(txtEstado.Text)
        If uf = "" Then
            faltando.Add(New KeyValuePair(Of String, Control)("UF", txtEstado))
        ElseIf Not UFsValidas.Contains(uf) Then
            faltando.Add(New KeyValuePair(Of String, Control)("UF válida", txtEstado))
        End If
        If LimparNome(txtVelocidade.Text) = "" Then faltando.Add(New KeyValuePair(Of String, Control)("velocidade", txtVelocidade))
        If Not dpData.SelectedDate.HasValue Then faltando.Add(New KeyValuePair(Of String, Control)("data", dpData))
        Return faltando
    End Function

    ' Borda vermelha nos campos que faltam (só depois de tentar exportar), cinza nos demais
    Private _mostrarFaltando As Boolean = False

    Private Sub DestacarCamposFaltando(faltando As List(Of KeyValuePair(Of String, Control)))
        For Each c As Control In {txtCliente, txtCidade, txtEstado, txtVelocidade, dpData}
            Dim falta = _mostrarFaltando AndAlso faltando.Any(Function(f) f.Value Is c)
            c.BorderBrush = If(falta, Paleta.Vermelho, Paleta.BordaForte)
        Next
    End Sub

    Private Shared Function JuntarLista(itens As List(Of String)) As String
        If itens.Count <= 1 Then Return String.Join("", itens)
        Return String.Join(", ", itens.Take(itens.Count - 1)) & " e " & itens.Last()
    End Function

    Private Sub Campo_TextChanged(sender As Object, e As TextChangedEventArgs)
        If _pronto Then AtualizarEstado()
    End Sub

    Private Sub Campo_DataChanged(sender As Object, e As SelectionChangedEventArgs)
        If _pronto Then AtualizarEstado()
    End Sub

    Public Sub New(settings As ExportSettings, qtdVideos As Integer, ignoradosCrono As Integer, temSegmentos As Boolean)
        InitializeComponent()
        _settings = settings
        _qtdVideos = qtdVideos
        _temSegmentos = temSegmentos

        ' Cabeçalho: quantidade e quem fica de fora
        lblQtdVideos.Text = If(qtdVideos = 1, "1 vídeo", $"{qtdVideos} vídeos")
        If ignoradosCrono > 0 Then
            chipIgnorados.Visibility = Visibility.Visible
            lblIgnorados.Text = If(ignoradosCrono = 1, "1 com cronoanálise fica de fora", $"{ignoradosCrono} com cronoanálise ficam de fora")
        End If

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

        chkPadronizada.IsChecked = settings.UsarPastaPadronizada
        txtCliente.Text = If(settings.PadronizadaCliente, "")
        txtCidade.Text = If(settings.PadronizadaCidade, "")
        txtEstado.Text = If(settings.PadronizadaEstado, "")
        txtVelocidade.Text = If(settings.PadronizadaVelocidade, "")
        Dim dataSalva As Date
        dpData.SelectedDate = If(Date.TryParseExact(settings.PadronizadaData, "yyyy-MM-dd", Globalization.CultureInfo.InvariantCulture,
                                                     Globalization.DateTimeStyles.None, dataSalva), dataSalva, Date.Today)

        _pronto = True
        AtualizarEstado()
    End Sub

    Private Sub Opcao_Changed(sender As Object, e As RoutedEventArgs)
        If _pronto Then AtualizarEstado()
    End Sub

    ' Cor de destaque de toda a tela conforme o desempenho: verde = econômico, vermelho = alto
    Private Sub AplicarCorDestaque(economico As Boolean)
        Dim principal, texto, fundo, chip As Color
        If Not economico Then
            principal = Paleta.Vermelho.Color : texto = Color.FromRgb(&HD7, &H0, &H15)
            fundo = Color.FromRgb(&HFF, &HF5, &HF4) : chip = Color.FromRgb(&HFF, &HE5, &HE3)
        Else
            principal = Paleta.Verde.Color : texto = Color.FromRgb(&H24, &H8A, &H3D)
            fundo = Color.FromRgb(&HF2, &HFB, &HF4) : chip = Paleta.VerdeClaro.Color
        End If
        Resources("CorDestaque") = New SolidColorBrush(principal)
        Resources("CorDestaqueTexto") = New SolidColorBrush(texto)
        Resources("CorDestaqueFundo") = New SolidColorBrush(fundo)
        Resources("CorDestaqueChip") = New SolidColorBrush(chip)
    End Sub

    Private Sub AtualizarEstado()
        Dim mesmaPasta = (rbPastaOriginal.IsChecked = True)
        Dim economico = (rbEconomico.IsChecked = True)
        AplicarCorDestaque(economico)

        ' A exclusão é sub-opção da "Mesma pasta": só aparece com ela escolhida
        pnlExcluir.Visibility = If(mesmaPasta, Visibility.Visible, Visibility.Collapsed)
        If Not mesmaPasta Then chkExcluirOriginal.IsChecked = False

        ' A pasta padronizada é sub-opção da "Outra pasta" e pode ser desligada
        pnlPadronizada.Visibility = If(mesmaPasta, Visibility.Collapsed, Visibility.Visible)
        Dim padronizada = Not mesmaPasta AndAlso chkPadronizada.IsChecked = True
        pnlCamposPadronizada.Visibility = If(padronizada, Visibility.Visible, Visibility.Collapsed)
        bdrPadronizada.BorderBrush = If(padronizada, CType(FindResource("CorDestaque"), Brush), Paleta.Borda)
        lblPadronizadaDesc.Text = If(padronizada,
            "Os vídeos vão para uma pasta nova dentro da pasta escolhida.",
            "Desligado: os vídeos vão direto para a pasta escolhida.")

        Dim faltando = CamposFaltando()
        DestacarCamposFaltando(faltando)
        Dim nomePasta = MontarNomePasta()
        If nomePasta = "" Then
            icoPrevia.Text = ChrW(&HE946).ToString()
            icoPrevia.Foreground = Paleta.Laranja
            bdrPrevia.Background = Paleta.LaranjaClaro
            lblPreviaTitulo.Text = "FALTA PREENCHER"
            Dim lista = JuntarLista(faltando.Select(Function(f) f.Key).ToList())
            lblNomePasta.Text = Char.ToUpper(lista(0)) & lista.Substring(1) & "."
        Else
            icoPrevia.Text = ChrW(&HE8B7).ToString()
            icoPrevia.Foreground = Paleta.TextoSecundario
            bdrPrevia.Background = Paleta.FundoCinza
            lblPreviaTitulo.Text = "NOME DA PASTA"
            lblNomePasta.Text = nomePasta
        End If

        Dim excluir = mesmaPasta AndAlso chkExcluirOriginal.IsChecked = True
        If excluir Then
            bdrExcluir.Background = Paleta.VermelhoClaro
            bdrExcluir.BorderBrush = Paleta.Vermelho
            lblExcluirDica.Text = If(_qtdVideos = 1, "O original será apagado. Não dá para desfazer.",
                                     $"Os {_qtdVideos} originais serão apagados. Não dá para desfazer.")
            btnExportar.Content = "Exportar e excluir originais"
            btnExportar.Background = Paleta.Vermelho
        Else
            bdrExcluir.Background = Brushes.White
            bdrExcluir.BorderBrush = Paleta.Borda
            lblExcluirDica.Text = "Apaga o arquivo original depois que a conversão der certo."
            btnExportar.Content = "Exportar"
            btnExportar.SetResourceReference(Button.BackgroundProperty, "CorDestaque")
        End If

        lblDescDesempenho.Text = If(rbEconomico.IsChecked = True, "Usa metade do processador, sem travar o PC.", "Usa todo o processador; termina mais rápido.")

        ' Rodapé: resumo em uma linha do que vai acontecer
        Dim partes As New List(Of String)
        If _temSegmentos Then partes.Add(If(rbUnico.IsChecked = True, "Segmentos juntos", "Um arquivo por segmento"))
        If mesmaPasta Then
            partes.Add(If(excluir, "Mesma pasta, apagando originais", "Mesma pasta"))
        Else
            partes.Add(If(String.IsNullOrEmpty(PastaDestino), "Outra pasta (a escolher)", $"Em {Path.GetFileName(PastaDestino.TrimEnd("\"c))}"))
            If padronizada Then partes.Add("em subpasta padronizada")
        End If
        partes.Add(If(rbEconomico.IsChecked = True, "Econômico", "Alto desempenho"))
        lblRodape.Text = String.Join(" · ", partes)
    End Sub

    Private Sub BtnEscolherPasta_Click(sender As Object, e As RoutedEventArgs)
        If EscolherPasta() Then
            rbOutraPasta.IsChecked = True
            AtualizarEstado()
        End If
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

        Dim usarPadronizada = rbOutraPasta.IsChecked = True AndAlso chkPadronizada.IsChecked = True
        If usarPadronizada AndAlso MontarNomePasta() = "" Then
            ' Destaca o que falta e leva o cursor ao primeiro campo, em vez de abrir um aviso
            _mostrarFaltando = True
            AtualizarEstado()
            CamposFaltando().First().Value.Focus()
            Return
        End If
        NomePastaPadronizada = If(usarPadronizada, MontarNomePasta(), "")

        ' Guarda os campos mesmo que a opção esteja desligada, para reaproveitar depois
        _settings.UsarPastaPadronizada = (chkPadronizada.IsChecked = True)
        _settings.PadronizadaCliente = txtCliente.Text.Trim()
        _settings.PadronizadaCidade = txtCidade.Text.Trim()
        _settings.PadronizadaEstado = txtEstado.Text.Trim()
        _settings.PadronizadaVelocidade = txtVelocidade.Text.Trim()
        _settings.PadronizadaData = If(dpData.SelectedDate.HasValue, dpData.SelectedDate.Value.ToString("yyyy-MM-dd"), "")

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
