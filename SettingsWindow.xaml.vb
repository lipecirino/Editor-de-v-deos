Imports System.Windows.Input
Imports System.Collections.ObjectModel

''' <summary>
''' ViewModel para cada linha da lista de atalhos.
''' </summary>
Public Class AtalhoItem
    Implements ComponentModel.INotifyPropertyChanged

    Public Event PropertyChanged As ComponentModel.PropertyChangedEventHandler Implements ComponentModel.INotifyPropertyChanged.PropertyChanged

    Public Property NomeAcao As String
    Public Property NomeExibicao As String
    Private _teclaAtual As String

    Public Property TeclaAtual As String
        Get
            Return _teclaAtual
        End Get
        Set(value As String)
            If value <> _teclaAtual Then
                _teclaAtual = value
                RaiseEvent PropertyChanged(Me, New ComponentModel.PropertyChangedEventArgs(NameOf(TeclaAtual)))
                RaiseEvent PropertyChanged(Me, New ComponentModel.PropertyChangedEventArgs(NameOf(TeclaExibicao)))
            End If
        End Set
    End Property

    Public ReadOnly Property TeclaExibicao As String
        Get
            Return FormatTeclaParaExibicao(_teclaAtual)
        End Get
    End Property

    Private Shared Function FormatTeclaParaExibicao(tecla As String) As String
        Select Case tecla
            Case "Left" : Return "← (Seta Esquerda)"
            Case "Right" : Return "→ (Seta Direita)"
            Case "Up" : Return "↑ (Seta Para Cima)"
            Case "Down" : Return "↓ (Seta Para Baixo)"
            Case "Insert" : Return "Insert"
            Case Else
                ' Para teclas comuns como A-Z, F1-F12, etc.
                Return tecla
        End Select
    End Function
End Class

''' <summary>
''' Janela de configuração de atalhos de teclado.
''' </summary>
Public Class SettingsWindow
    Inherits Window

    Private _atalhos As New ObservableCollection(Of AtalhoItem)()
    Private _settings As KeyboardShortcutSettings
    Private _itemEmEdicao As AtalhoItem = Nothing
    Private _borderEmEdicao As Border = Nothing

    Public Sub New()
        InitializeComponent()

        ' Carregar configurações atuais
        _settings = SettingsManager.Carregar()

        ' Popular lista
        _atalhos.Add(New AtalhoItem() With {
            .NomeAcao = "FrameAnterior",
            .NomeExibicao = "Frame Anterior",
            .TeclaAtual = _settings.Atalhos("FrameAnterior")
        })
        _atalhos.Add(New AtalhoItem() With {
            .NomeAcao = "FrameProximo",
            .NomeExibicao = "Próximo Frame",
            .TeclaAtual = _settings.Atalhos("FrameProximo")
        })
        _atalhos.Add(New AtalhoItem() With {
            .NomeAcao = "PlayPause",
            .NomeExibicao = "Play / Pause",
            .TeclaAtual = _settings.Atalhos("PlayPause")
        })
        _atalhos.Add(New AtalhoItem() With {
            .NomeAcao = "RegistrarTempo",
            .NomeExibicao = "Registrar Tempo (Crono)",
            .TeclaAtual = _settings.Atalhos("RegistrarTempo")
        })
        _atalhos.Add(New AtalhoItem() With {
            .NomeAcao = "NovaOperacao",
            .NomeExibicao = "Nova Operação (Crono)",
            .TeclaAtual = _settings.Atalhos("NovaOperacao")
        })
        _atalhos.Add(New AtalhoItem() With {
            .NomeAcao = "VelocidadeMais",
            .NomeExibicao = "Aumentar Velocidade",
            .TeclaAtual = _settings.Atalhos("VelocidadeMais")
        })
        _atalhos.Add(New AtalhoItem() With {
            .NomeAcao = "VelocidadeMenos",
            .NomeExibicao = "Diminuir Velocidade",
            .TeclaAtual = _settings.Atalhos("VelocidadeMenos")
        })

        listaAtalhos.ItemsSource = _atalhos

        ' Registrar preview key down global na janela
        AddHandler Me.PreviewKeyDown, AddressOf SettingsWindow_PreviewKeyDown

        ' Registrar preview mouse down para capturar clique nos borders
        AddHandler Me.PreviewMouseDown, AddressOf SettingsWindow_PreviewMouseDown
    End Sub

    Private Sub SettingsWindow_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs)
        ' Verificar se clicou em um Border de tecla
        Dim originalSrc = TryCast(e.OriginalSource, DependencyObject)
        If originalSrc Is Nothing Then Return

        ' Procurar o Border mais próximo no visual tree
        Dim border = FindParent(Of Border)(originalSrc)
        If border IsNot Nothing AndAlso border.Tag IsNot Nothing Then
            Dim item = TryCast(border.Tag, AtalhoItem)
            If item IsNot Nothing Then
                IniciarEdicao(item, border)
                e.Handled = True
                Return
            End If
        End If

        ' Se clicar fora, finalizar edição sem alteração
        FinalizarEdicaoSemAlteracao()
    End Sub

    Private Sub IniciarEdicao(item As AtalhoItem, border As Border)
        ' Finalizar edição anterior se houver
        FinalizarEdicaoSemAlteracao()

        _itemEmEdicao = item
        _borderEmEdicao = border
        border.Background = New System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(&HDD, &HEB, &HFF))
        border.BorderBrush = New System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(&H33, &H99, &HFF))
        border.BorderThickness = New Thickness(2)
        border.ToolTip = "Aguardando tecla... Pressione uma tecla"
    End Sub

    Private Sub FinalizarEdicaoSemAlteracao()
        If _borderEmEdicao IsNot Nothing Then
            _borderEmEdicao.Background = New System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(&HF1, &HF4, &HF8))
            _borderEmEdicao.BorderBrush = New System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(&HCC, &HD6, &HE2))
            _borderEmEdicao.BorderThickness = New Thickness(1)
            _borderEmEdicao.ToolTip = "Clique e pressione a tecla desejada"
            _borderEmEdicao = Nothing
        End If
        _itemEmEdicao = Nothing
    End Sub

    Private Sub SettingsWindow_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        If _itemEmEdicao Is Nothing Then
            ' Se não estiver editando, Enter fecha a janela como salvar, Escape como cancelar
            If e.Key = Key.Escape Then
                DialogResult = False
                Close()
            End If
            Return
        End If

        ' Capturar a tecla pressionada (ignorando modificadores isolados)
        Dim tecla = e.Key

        ' Ignorar teclas modificadoras sozinhas
        If tecla = Key.LeftCtrl OrElse tecla = Key.RightCtrl OrElse
           tecla = Key.LeftShift OrElse tecla = Key.RightShift OrElse
           tecla = Key.LeftAlt OrElse tecla = Key.RightAlt OrElse
           tecla = Key.LWin OrElse tecla = Key.RWin Then
            e.Handled = True
            Return
        End If

        e.Handled = True

        ' Validar se a tecla já está em uso por outro atalho
        Dim conflito = _atalhos.FirstOrDefault(Function(a) a IsNot _itemEmEdicao AndAlso a.TeclaAtual = tecla.ToString())
        If conflito IsNot Nothing Then
            Dim resultado = MessageBox.Show(
                $"A tecla '{FormatTecla(tecla.ToString())}' já está atribuída a '{conflito.NomeExibicao}'.{vbCrLf}Deseja substituí-la?",
                "Conflito de Atalho",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning)

            If resultado = MessageBoxResult.No Then
                FinalizarEdicaoSemAlteracao()
                Return
            End If

            ' Remover do atalho conflitante
            conflito.TeclaAtual = ""
        End If

        ' Atribuir a nova tecla
        _itemEmEdicao.TeclaAtual = tecla.ToString()
        FinalizarEdicaoSemAlteracao()
    End Sub

    Private Shared Function FormatTecla(tecla As String) As String
        Select Case tecla
            Case "Left" : Return "Seta Esquerda"
            Case "Right" : Return "Seta Direita"
            Case "Up" : Return "Seta Para Cima"
            Case "Down" : Return "Seta Para Baixo"
            Case "Insert" : Return "Insert"
            Case Else : Return tecla
        End Select
    End Function

    Private Shared Function FindParent(Of T As DependencyObject)(child As DependencyObject) As T
        While child IsNot Nothing
            If TypeOf child Is T Then
                Return DirectCast(child, T)
            End If
            child = VisualTreeHelper.GetParent(child)
        End While
        Return Nothing
    End Function

    Private Sub BtnSalvar_Click(sender As Object, e As RoutedEventArgs)
        ' Finalizar qualquer edição pendente
        FinalizarEdicaoSemAlteracao()

        ' Aplicar valores ao settings
        For Each item In _atalhos
            If Not String.IsNullOrWhiteSpace(item.TeclaAtual) Then
                _settings.Atalhos(item.NomeAcao) = item.TeclaAtual
            End If
        Next

        ' Salvar
        SettingsManager.Salvar(_settings)
        DialogResult = True
        Close()
    End Sub

    Private Sub BtnCancelar_Click(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub

    Private Sub BtnRestaurarPadroes_Click(sender As Object, e As RoutedEventArgs)
        Dim resultado = MessageBox.Show(
            "Restaurar todos os atalhos para os valores padrão?",
            "Restaurar Padrões",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question)

        If resultado = MessageBoxResult.Yes Then
            _settings = New KeyboardShortcutSettings()
            For Each item In _atalhos
                If _settings.Atalhos.ContainsKey(item.NomeAcao) Then
                    item.TeclaAtual = _settings.Atalhos(item.NomeAcao)
                End If
            Next
        End If
    End Sub
End Class
