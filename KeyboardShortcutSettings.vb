Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization

''' <summary>
''' Modelo de configuração de atalhos de teclado.
''' </summary>
Public Class KeyboardShortcutSettings
    Public Property Atalhos As New Dictionary(Of String, String)() From {
        {"FrameAnterior", "Left"},
        {"FrameProximo", "Right"},
        {"PlayPause", "Down"},
        {"RegistrarTempo", "Up"},
        {"NovaOperacao", "Insert"},
        {"VelocidadeMais", "O"},
        {"VelocidadeMenos", "P"}
    }
End Class

''' <summary>
''' Gerenciador para carregar/salvar configurações de atalhos no AppData do usuário.
''' </summary>
Public Class SettingsManager
    Private Shared ReadOnly _caminhoArquivo As String
    Private Shared _cache As KeyboardShortcutSettings = Nothing

    Shared Sub New()
        Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Dim pastaApp = Path.Combine(appData, "EditorDeVideos")
        Directory.CreateDirectory(pastaApp)
        _caminhoArquivo = Path.Combine(pastaApp, "shortcuts.json")
    End Sub

    ''' <summary>
    ''' Carrega as configurações do disco. Se o arquivo não existir, retorna os padrões.
    ''' </summary>
    Public Shared Function Carregar() As KeyboardShortcutSettings
        If _cache IsNot Nothing Then Return _cache

        Try
            If File.Exists(_caminhoArquivo) Then
                Dim json = File.ReadAllText(_caminhoArquivo, Text.Encoding.UTF8)
                Dim options = New JsonSerializerOptions() With {.PropertyNameCaseInsensitive = True}
                Dim settings = JsonSerializer.Deserialize(Of KeyboardShortcutSettings)(json, options)
                If settings IsNot Nothing Then
                    ' Garantir que todas as chaves padrão existam (preencher se faltar)
                    PreencherPadroesFaltantes(settings)
                    _cache = settings
                    Return _cache
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"Erro ao carregar configurações de atalhos: {ex.Message}")
        End Try

        _cache = New KeyboardShortcutSettings()
        Return _cache
    End Function

    ''' <summary>
    ''' Salva as configurações no disco.
    ''' </summary>
    Public Shared Sub Salvar(settings As KeyboardShortcutSettings)
        Try
            Dim options = New JsonSerializerOptions() With {
                .WriteIndented = True,
                .PropertyNameCaseInsensitive = True
            }
            Dim json = JsonSerializer.Serialize(settings, options)
            File.WriteAllText(_caminhoArquivo, json, Text.Encoding.UTF8)
            _cache = settings
        Catch ex As Exception
            MessageBox.Show($"Erro ao salvar configurações de atalhos: {ex.Message}",
                            "Erro", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    ''' <summary>
    ''' Retorna o caminho completo do arquivo de configuração.
    ''' </summary>
    Public Shared ReadOnly Property CaminhoArquivo As String
        Get
            Return _caminhoArquivo
        End Get
    End Property

    ''' <summary>
    ''' Obtém a tecla configurada para uma ação. Se a ação não existir, retorna o padrão fornecido.
    ''' </summary>
    Public Shared Function ObterTecla(nomeAcao As String, Optional teclaPadrao As String = "") As String
        Dim settings = Carregar()
        If settings.Atalhos.ContainsKey(nomeAcao) Then
            Return settings.Atalhos(nomeAcao)
        End If
        Return teclaPadrao
    End Function

    Private Shared Sub PreencherPadroesFaltantes(settings As KeyboardShortcutSettings)
        Dim defaults As New Dictionary(Of String, String)() From {
            {"FrameAnterior", "Left"},
            {"FrameProximo", "Right"},
            {"PlayPause", "Down"},
            {"RegistrarTempo", "Up"},
            {"NovaOperacao", "Insert"},
            {"VelocidadeMais", "O"},
            {"VelocidadeMenos", "P"}
        }

        Dim modificado As Boolean = False
        For Each kv In defaults
            If Not settings.Atalhos.ContainsKey(kv.Key) Then
                settings.Atalhos(kv.Key) = kv.Value
                modificado = True
            End If
        Next

        ' Remover chaves obsoletas (que não estão nos defaults)
        Dim chavesParaRemover = settings.Atalhos.Keys.Where(Function(k) Not defaults.ContainsKey(k)).ToList()
        For Each chave In chavesParaRemover
            settings.Atalhos.Remove(chave)
            modificado = True
        Next

        If modificado Then
            Salvar(settings)
        End If
    End Sub
End Class
