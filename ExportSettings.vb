Imports System.IO
Imports System.Text.Json
Imports System.Text.Json.Serialization

''' <summary>
''' Modelo de configuração de exportação.
''' </summary>
Public Class ExportSettings
    Public Property SalvarNaPastaOriginal As Boolean = True
    Public Property DeletarOriginal As Boolean = False
    Public Property ModoEconomico As Boolean = False
End Class

''' <summary>
''' Gerenciador para carregar/salvar configurações de exportação no AppData do usuário.
''' </summary>
Public Class ExportSettingsManager
    Private Shared ReadOnly _caminhoArquivo As String
    Private Shared _cache As ExportSettings = Nothing

    Shared Sub New()
        Dim appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        Dim pastaApp = Path.Combine(appData, "EditorDeVideos")
        Directory.CreateDirectory(pastaApp)
        _caminhoArquivo = Path.Combine(pastaApp, "export_settings.json")
    End Sub

    ''' <summary>
    ''' Carrega as configurações do disco. Se o arquivo não existir, retorna os padrões.
    ''' </summary>
    Public Shared Function Carregar() As ExportSettings
        If _cache IsNot Nothing Then Return _cache

        Try
            If File.Exists(_caminhoArquivo) Then
                Dim json = File.ReadAllText(_caminhoArquivo, Text.Encoding.UTF8)
                Dim options = New JsonSerializerOptions() With {.PropertyNameCaseInsensitive = True}
                Dim settings = JsonSerializer.Deserialize(Of ExportSettings)(json, options)
                If settings IsNot Nothing Then
                    _cache = settings
                    Return _cache
                End If
            End If
        Catch ex As Exception
            Debug.WriteLine($"Erro ao carregar configurações de exportação: {ex.Message}")
        End Try

        _cache = New ExportSettings()
        Return _cache
    End Function

    ''' <summary>
    ''' Salva as configurações no disco.
    ''' </summary>
    Public Shared Sub Salvar(settings As ExportSettings)
        Try
            Dim options = New JsonSerializerOptions() With {
                .WriteIndented = True,
                .PropertyNameCaseInsensitive = True
            }
            Dim json = JsonSerializer.Serialize(settings, options)
            File.WriteAllText(_caminhoArquivo, json, Text.Encoding.UTF8)
            _cache = settings
        Catch ex As Exception
            MessageBox.Show($"Erro ao salvar configurações de exportação: {ex.Message}",
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
End Class
