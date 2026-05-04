Imports FFMpegCore
Imports Microsoft.Win32
Imports System.Windows
Imports System.Windows.Threading
Imports System.IO
Imports System.Windows.Controls.Primitives ' NECESSÁRIO PARA OS EVENTOS DE ARRASTE (DRAG)
Imports System.Windows.Input ' NECESSÁRIO PARA DETECÇÃO DE TECLAS
Imports System.Windows.Media.Animation ' RepeatBehavior, MediaTimeline, MediaClock
Imports System.Windows.Media ' TimeSeekOrigin

' --- CLASSE DE DADOS INDIVIDUAIS ---
Public Class VideoTarefa
    Public Property Caminho As String
    Public Property Inicio As Double = 0
    Public Property Fim As Double = 0
    Public Property FiltroFFmpeg As String = ""
    Public Overrides Function ToString() As String
        Return Path.GetFileName(Caminho)
    End Function
End Class

Class MainWindow
    Private cronometroVideo As New DispatcherTimer()
    Private videoAtual As VideoTarefa = Nothing
    Private mediaTimeline As MediaTimeline
    Private mediaClock As MediaClock

    Private estaReproduzindo As Boolean = False
    Private isDraggingSlider As Boolean = False
    Private frameRate As Double = 30.0 ' Taxa de quadros padrão (será atualizada ao abrir o vídeo)

    Public Sub New()
        InitializeComponent()
        ' Configura FFmpeg na pasta do app
        GlobalFFOptions.Configure(New FFOptions With {.BinaryFolder = AppDomain.CurrentDomain.BaseDirectory})
        cronometroVideo.Interval = TimeSpan.FromMilliseconds(200)
        AddHandler cronometroVideo.Tick, AddressOf CronometroTick
    End Sub

    ' --- SALVAMENTO AUTOMÁTICO DE SETTINGS ---
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

    ' --- GALERIA E ARQUIVOS ---
    Private Sub BtnAbrirArquivos_Click(sender As Object, e As RoutedEventArgs)
        Dim abrir As New OpenFileDialog() With {.Multiselect = True, .Filter = "Vídeos|*.mp4;*.avi;*.mkv;*.mov;*.3gp"}
        If abrir.ShowDialog() = True Then
            For Each arq In abrir.FileNames
                lstGaleria.Items.Add(New VideoTarefa() With {.Caminho = arq})
            Next
        End If
    End Sub

    Private Sub BtnAbrirPasta_Click(sender As Object, e As RoutedEventArgs)
        Using sel As New System.Windows.Forms.FolderBrowserDialog()
            If sel.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
                Dim ext = {".mp4", ".avi", ".mkv", ".mov", ".3gp"}
                For Each arq In Directory.GetFiles(sel.SelectedPath, "*.*", SearchOption.AllDirectories)
                    If ext.Contains(Path.GetExtension(arq).ToLower()) Then
                        lstGaleria.Items.Add(New VideoTarefa() With {.Caminho = arq})
                    End If
                Next
            End If
        End Using
    End Sub

    Private Sub LstGaleria_SelectionChanged(sender As Object, e As SelectionChangedEventArgs)
        SalvarConfiguracoesAtuais()
        If lstGaleria.SelectedItem IsNot Nothing Then
            videoAtual = DirectCast(lstGaleria.SelectedItem, VideoTarefa)

            ' Para e limpa o clock anterior
            If mediaClock IsNot Nothing Then
                mediaClock.Controller.Stop()
                VisualizadorVideo.Clock = Nothing
                mediaClock = Nothing
            End If

            ' Cria o MediaTimeline com clock controlável (permite SpeedRatio e Seek)
            mediaTimeline = New MediaTimeline(New Uri(videoAtual.Caminho))
            mediaTimeline.RepeatBehavior = New RepeatBehavior(1)
            mediaClock = mediaTimeline.CreateClock(True)
            VisualizadorVideo.Clock = mediaClock
            mediaClock.Controller.Begin()

            btnPlayPause.Content = "⏸"
            estaReproduzindo = True

            txtInicio.Text = videoAtual.Inicio.ToString()
            txtFim.Text = videoAtual.Fim.ToString()
            rbNormal.IsChecked = (videoAtual.FiltroFFmpeg = "")
            rbRotDireita.IsChecked = (videoAtual.FiltroFFmpeg = "-vf transpose=1")
            rbRotEsquerda.IsChecked = (videoAtual.FiltroFFmpeg = "-vf transpose=2")
            rbRot180.IsChecked = (videoAtual.FiltroFFmpeg = "-vf transpose=2,transpose=2")
            rbEspelharH.IsChecked = (videoAtual.FiltroFFmpeg = "-vf hflip")
            rbEspelharV.IsChecked = (videoAtual.FiltroFFmpeg = "-vf vflip")
        End If
    End Sub

    Private Sub BtnLimpar_Click(sender As Object, e As RoutedEventArgs)
        lstGaleria.Items.Clear()
        If mediaClock IsNot Nothing Then
            mediaClock.Controller.Stop()
            VisualizadorVideo.Clock = Nothing
            mediaClock = Nothing
        End If
        mediaTimeline = Nothing
        videoAtual = Nothing
        estaReproduzindo = False
        btnPlayPause.Content = "▶"
    End Sub

    ' --- CONTROLES DE REPRODUÇÃO E LINHA DO TEMPO ---
    Private Sub BtnPlayPause_Click(sender As Object, e As RoutedEventArgs)
        If mediaClock Is Nothing Then Return
        If estaReproduzindo Then
            mediaClock.Controller.Pause()
            btnPlayPause.Content = "▶"
        Else
            mediaClock.Controller.Resume()
            btnPlayPause.Content = "⏸"
        End If
        estaReproduzindo = Not estaReproduzindo
    End Sub

    Private Sub VisualizadorVideo_MediaOpened(sender As Object, e As RoutedEventArgs)
        If VisualizadorVideo.NaturalDuration.HasTimeSpan Then
            slLinhaTempo.Maximum = VisualizadorVideo.NaturalDuration.TimeSpan.TotalSeconds
            lblTempoTotal.Text = VisualizadorVideo.NaturalDuration.TimeSpan.ToString("hh\:mm\:ss")
            cronometroVideo.Start()

            ' Tenta obter o frame rate do vídeo
            Try
                Dim info = FFProbe.Analyse(videoAtual.Caminho)
                If info.VideoStreams.Count > 0 Then
                    frameRate = info.VideoStreams(0).AvgFrameRate
                End If
            Catch
                frameRate = 30.0 ' Usa padrão se não conseguir obter
            End Try
        End If
    End Sub

    Private Sub CronometroTick(sender As Object, e As EventArgs)
        If mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue Then Return
        ' Atualiza a barra SÓ SE não estiver arrastando com o mouse
        If Not isDraggingSlider Then
            Dim pos = mediaClock.CurrentTime.Value.TotalSeconds
            slLinhaTempo.Value = pos
            lblTempoAtual.Text = mediaClock.CurrentTime.Value.ToString("hh\:mm\:ss")
        End If
    End Sub

    ' Evento NATIVO de quando clica e segura a bolinha
    Private Sub SlLinhaTempo_DragStarted(sender As Object, e As DragStartedEventArgs)
        isDraggingSlider = True
        If mediaClock IsNot Nothing Then mediaClock.Controller.Pause()
        popupTempoDrag.IsOpen = True
    End Sub

    ' Evento NATIVO de quando solta a bolinha
    Private Sub SlLinhaTempo_DragCompleted(sender As Object, e As DragCompletedEventArgs)
        isDraggingSlider = False
        popupTempoDrag.IsOpen = False
        If mediaClock IsNot Nothing Then
            mediaClock.Controller.Resume()
            mediaClock.Controller.Seek(TimeSpan.FromSeconds(slLinhaTempo.Value), TimeSeekOrigin.BeginTime)
            lblTempoAtual.Text = TimeSpan.FromSeconds(slLinhaTempo.Value).ToString("hh\:mm\:ss")
            If Not estaReproduzindo Then
                Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                           mediaClock.Controller.Pause()
                                                                       End Sub)
            End If
        End If
    End Sub

    ' Atualiza o popup de tempo enquanto arrasta
    Private Sub SlLinhaTempo_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If isDraggingSlider Then
            Dim t = TimeSpan.FromSeconds(slLinhaTempo.Value)
            lblTempoDrag.Text = t.ToString("hh\:mm\:ss")
            ' Força o popup a seguir o mouse
            popupTempoDrag.HorizontalOffset += 0.1
            popupTempoDrag.HorizontalOffset -= 0.1
        End If
    End Sub

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

    ' --- PROCESSAMENTO EM LOTE ---
    Private Async Sub BtnProcessar_Click(sender As Object, e As RoutedEventArgs)
        If lstGaleria.Items.Count = 0 Then Return
        SalvarConfiguracoesAtuais()

        Using sel As New System.Windows.Forms.FolderBrowserDialog()
            If sel.ShowDialog() = System.Windows.Forms.DialogResult.OK Then
                Dim destino = sel.SelectedPath
                btnProcessar.IsEnabled = False
                painelProgresso.Visibility = Visibility.Visible
                If mediaClock IsNot Nothing Then mediaClock.Controller.SpeedRatio = 0
                btnPlayPause.Content = "▶"
                estaReproduzindo = False

                Try
                    For i = 0 To lstGaleria.Items.Count - 1
                        ' Cópias locais para evitar o erro do Lambda no loop
                        Dim indiceAtual As Integer = i
                        Dim tarefa As VideoTarefa = DirectCast(lstGaleria.Items(indiceAtual), VideoTarefa)

                        Dim info = FFProbe.Analyse(tarefa.Caminho)
                        Dim saida = Path.Combine(destino, Path.GetFileNameWithoutExtension(tarefa.Caminho) & "_editado.mp4")

                        Dim bitrateOriginal = info.Format.BitRate / 1000
                        Dim bitrateAlvo As Double = If(bitrateOriginal > 6000, 6000, 0)

                        Dim duracao As TimeSpan = If(tarefa.Fim > tarefa.Inicio,
                            TimeSpan.FromSeconds(tarefa.Fim - tarefa.Inicio),
                            info.Duration - TimeSpan.FromSeconds(tarefa.Inicio))

                        ' Filtro de número inteiro para não travar a UI
                        Dim ultimaPorcentagem As Integer = -1

                        Dim engine = FFMpegArguments.FromFileInput(tarefa.Caminho).
                            OutputToFile(saida, True, Sub(opt)
                                                          If bitrateAlvo > 0 Then opt.WithVideoBitrate(bitrateAlvo)
                                                          If tarefa.Inicio > 0 Then opt.Seek(TimeSpan.FromSeconds(tarefa.Inicio))
                                                          If tarefa.Fim > tarefa.Inicio Then opt.WithDuration(TimeSpan.FromSeconds(tarefa.Fim - tarefa.Inicio))
                                                          If Not String.IsNullOrEmpty(tarefa.FiltroFFmpeg) Then opt.WithCustomArgument(tarefa.FiltroFFmpeg)
                                                      End Sub).
                            NotifyOnProgress(Sub(perc)
                                                 Dim porcentagemAtual As Integer = CInt(Math.Round(perc, 0))

                                                 ' Atualiza somente quando muda o número inteiro
                                                 If porcentagemAtual <> ultimaPorcentagem Then
                                                     ultimaPorcentagem = porcentagemAtual

                                                     ' BeginInvoke trabalha em background sem travar a thread de conversão
                                                     Dispatcher.BeginInvoke(Sub()
                                                                                pbProgresso.Value = perc
                                                                                lblPorcentagem.Text = porcentagemAtual.ToString() & "%"
                                                                                lblStatusLote.Text = $"Convertendo {indiceAtual + 1}/{lstGaleria.Items.Count} - {Path.GetFileName(tarefa.Caminho)}"
                                                                            End Sub)
                                                 End If
                                             End Sub, duracao)

                        Await engine.ProcessAsynchronously()
                    Next
                    MessageBox.Show("Lote concluído com sucesso!", "Finalizado", MessageBoxButton.OK, MessageBoxImage.Information)
                Catch ex As Exception
                    MessageBox.Show("Erro: " & ex.Message, "Falha", MessageBoxButton.OK, MessageBoxImage.Error)
                Finally
                    btnProcessar.IsEnabled = True
                    painelProgresso.Visibility = Visibility.Collapsed
                End Try
            End If
        End Using
    End Sub

    ' --- CONTROLE DE FRAMES ---
    Private Sub BtnFrameAnterior_Click(sender As Object, e As RoutedEventArgs)
        AvancarFrame(-1)
    End Sub

    Private Sub BtnFrameProximo_Click(sender As Object, e As RoutedEventArgs)
        AvancarFrame(1)
    End Sub

    Private Sub AvancarFrame(direcao As Integer)
        If mediaClock Is Nothing OrElse Not mediaClock.CurrentTime.HasValue Then Return

        ' Garante que está pausado antes de navegar
        If estaReproduzindo Then
            mediaClock.Controller.Pause()
            estaReproduzindo = False
            btnPlayPause.Content = "▶"
        End If

        Dim duracaoFrame As Double = 1.0 / frameRate
        Dim novaPosicao As Double = mediaClock.CurrentTime.Value.TotalSeconds + (duracaoFrame * direcao)

        If novaPosicao < 0 Then novaPosicao = 0
        If novaPosicao > slLinhaTempo.Maximum Then novaPosicao = slLinhaTempo.Maximum

        ' Resume → Seek: ativa o pipeline de render para mostrar o frame
        ' BeginInvoke(Background) re-pausa após o ciclo de renderização completar
        mediaClock.Controller.Resume()
        mediaClock.Controller.Seek(TimeSpan.FromSeconds(novaPosicao), TimeSeekOrigin.BeginTime)
        Dispatcher.BeginInvoke(DispatcherPriority.Background, Sub()
                                                                   If Not estaReproduzindo Then
                                                                       mediaClock.Controller.Pause()
                                                                   End If
                                                               End Sub)

        slLinhaTempo.Value = novaPosicao
        lblTempoAtual.Text = TimeSpan.FromSeconds(novaPosicao).ToString("hh\:mm\:ss")
    End Sub

    ' --- CONTROLE DE TECLADO (PreviewKeyDown captura antes do Slider consumir) ---
    Private Sub Window_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If e.Key = Key.Right Then
            AvancarFrame(1)
            e.Handled = True
        ElseIf e.Key = Key.Left Then
            AvancarFrame(-1)
            e.Handled = True
        End If
    End Sub
End Class