Imports System.IO
Imports FFMpegCore

Public Class VideoPropertiesWindow

    Private _caminhoVideo As String

    Public Sub New(caminhoVideo As String, owner As Window)
        InitializeComponent()
        Me.Owner = owner
        _caminhoVideo = caminhoVideo
        AddHandler Me.Loaded, AddressOf JanelaLoaded
    End Sub

    Private Sub JanelaLoaded(sender As Object, e As RoutedEventArgs)
        CarregarPropriedades(_caminhoVideo)
    End Sub

    Private Sub CarregarPropriedades(caminho As String)
        Try
            Dim fi As New FileInfo(caminho)

            ' --- Cabeçalho ---
            lblNomeArquivo.Text = fi.Name
            lblTipoArquivo.Text = $"Arquivo de vídeo ({fi.Extension.TrimStart(".").ToUpper()})"

            ' --- Aba Geral ---
            propNome.Text = fi.Name
            propTipo.Text = $"Arquivo de vídeo ({fi.Extension.TrimStart(".").ToUpper()})"
            propLocalizacao.Text = fi.DirectoryName
            propTamanho.Text = FormatarTamanho(fi.Length)
            propTamanhoBytes.Text = $"{fi.Length:#,##0} bytes"
            propCriadoEm.Text = fi.CreationTime.ToString("dd/MM/yyyy HH:mm:ss")
            propModificadoEm.Text = fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm:ss")
            propAcessadoEm.Text = fi.LastAccessTime.ToString("dd/MM/yyyy HH:mm:ss")

            Dim atributos As New List(Of String)()
            If fi.Attributes.HasFlag(FileAttributes.ReadOnly) Then atributos.Add("Somente leitura")
            If fi.Attributes.HasFlag(FileAttributes.Hidden) Then atributos.Add("Oculto")
            If fi.Attributes.HasFlag(FileAttributes.Archive) Then atributos.Add("Arquivo")
            If fi.Attributes.HasFlag(FileAttributes.System) Then atributos.Add("Sistema")
            propAtributos.Text = If(atributos.Count > 0, String.Join(", ", atributos), "Nenhum")

            ' --- FFProbe ---
            Dim info = FFProbe.Analyse(caminho)

            ' --- Aba Vídeo ---
            Dim dur As TimeSpan = info.Duration
            propDuracao.Text = $"{CInt(Math.Floor(dur.TotalHours)):D2}:{dur.Minutes:D2}:{dur.Seconds:D2},{dur.Milliseconds:D3}"

            Dim vs = info.PrimaryVideoStream
            If vs IsNot Nothing Then
                propResolucao.Text = $"{vs.Width} × {vs.Height} pixels"
                propLargura.Text = $"{vs.Width} pixels"
                propAltura.Text = $"{vs.Height} pixels"
                propFps.Text = $"{vs.FrameRate:0.###} fps"
                propCodecVideo.Text = If(String.IsNullOrEmpty(vs.CodecLongName), vs.CodecName, $"{vs.CodecName} ({vs.CodecLongName})")
                propPerfil.Text = If(String.IsNullOrWhiteSpace(vs.Profile), "—", vs.Profile)
                propBitrateVideo.Text = If(vs.BitRate > 0, FormatarBitrate(vs.BitRate), "—")
                propFormatoPixel.Text = If(String.IsNullOrWhiteSpace(vs.PixelFormat), "—", vs.PixelFormat)
                propRotacao.Text = If(vs.Rotation <> 0, $"{vs.Rotation}°", "0°")

                If vs.Width > 0 AndAlso vs.Height > 0 Then
                    Dim mdc As Integer = CalcularMDC(vs.Width, vs.Height)
                    propAspectRatio.Text = $"{vs.Width \ mdc}:{vs.Height \ mdc}"
                Else
                    propAspectRatio.Text = "—"
                End If
            Else
                propResolucao.Text = "—"
                propLargura.Text = "—"
                propAltura.Text = "—"
                propFps.Text = "—"
                propCodecVideo.Text = "—"
                propPerfil.Text = "—"
                propBitrateVideo.Text = "—"
                propFormatoPixel.Text = "—"
                propRotacao.Text = "—"
                propAspectRatio.Text = "—"
            End If

            propBitrateTotal.Text = If(info.Format.BitRate > 0, FormatarBitrate(info.Format.BitRate), "—")

            ' --- Aba Áudio ---
            Dim as_ = info.PrimaryAudioStream
            If as_ IsNot Nothing Then
                propCodecAudio.Text = If(String.IsNullOrEmpty(as_.CodecLongName), as_.CodecName, $"{as_.CodecName} ({as_.CodecLongName})")
                propCanais.Text = as_.Channels.ToString()
                propLayoutCanais.Text = If(String.IsNullOrWhiteSpace(as_.ChannelLayout), "—", as_.ChannelLayout)
                propTaxaAmostragem.Text = If(as_.SampleRateHz > 0, $"{as_.SampleRateHz:#,##0} Hz", "—")
                propBitrateAudio.Text = If(as_.BitRate > 0, FormatarBitrate(as_.BitRate), "—")
                Dim idioma As String = "—"
                If as_.Tags IsNot Nothing AndAlso as_.Tags.ContainsKey("language") Then
                    idioma = as_.Tags("language")
                End If
                propIdioma.Text = idioma
            Else
                propCodecAudio.Text = "Sem faixa de áudio"
                propCanais.Text = "—"
                propLayoutCanais.Text = "—"
                propTaxaAmostragem.Text = "—"
                propBitrateAudio.Text = "—"
                propIdioma.Text = "—"
            End If

        Catch ex As Exception
            Dispatcher.BeginInvoke(New Action(AddressOf Me.Close))
        End Try
    End Sub

    Private Shared Function FormatarTamanho(bytes As Long) As String
        If bytes >= 1073741824L Then
            Return $"{bytes / 1073741824.0:0.00} GB ({bytes:#,##0} bytes)"
        ElseIf bytes >= 1048576L Then
            Return $"{bytes / 1048576.0:0.00} MB ({bytes:#,##0} bytes)"
        ElseIf bytes >= 1024L Then
            Return $"{bytes / 1024.0:0.00} KB ({bytes:#,##0} bytes)"
        Else
            Return $"{bytes} bytes"
        End If
    End Function

    Private Shared Function FormatarBitrate(bps As Long) As String
        If bps >= 1000000 Then
            Return $"{bps / 1000000.0:0.##} Mbps"
        ElseIf bps >= 1000 Then
            Return $"{bps / 1000.0:0.##} kbps"
        Else
            Return $"{bps} bps"
        End If
    End Function

    Private Shared Function CalcularMDC(a As Integer, b As Integer) As Integer
        While b <> 0
            Dim t = b
            b = a Mod b
            a = t
        End While
        Return If(a = 0, 1, a)
    End Function

    Private Sub BtnFechar_Click(sender As Object, e As RoutedEventArgs)
        Me.Close()
    End Sub

End Class
