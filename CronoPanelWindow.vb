Imports System.Windows

Namespace Global
    ' Janela flutuante para hospedar o painel de cronoanálise
    Public Class CronoPanelWindow
        Inherits Window

        Private _mainWindow As MainWindow
        Private _cronoPanel As Border

        Public Sub New(mainWindow As MainWindow, cronoPanel As Border)
            Me._mainWindow = mainWindow
            Me._cronoPanel = cronoPanel

            ' Configurar a janela flutuante
            Me.Title = "Cronoanálise"
            Me.Width = 1000
            Me.Height = 350
            Me.WindowStartupLocation = WindowStartupLocation.CenterOwner
            Me.Owner = mainWindow

            ' Criar o container para o painel
            Dim grid = New Grid()
            grid.RowDefinitions.Add(New RowDefinition() With {.Height = New GridLength(1, GridUnitType.Star)})

            ' Mover o painel para dentro da janela
            Dim parent = VisualTreeHelper.GetParent(_cronoPanel)
            If TypeOf parent Is Panel Then
                CType(parent, Panel).Children.Remove(_cronoPanel)
            End If

            ' Adicionar o painel à janela
            Grid.SetRow(_cronoPanel, 0)
            grid.Children.Add(_cronoPanel)
            Me.Content = grid

            ' Quando a janela fechar, re-acoplar o painel
            AddHandler Me.Closed, Sub()
                                       _mainWindow.ReacoplarCronoPanel(_cronoPanel)
                                   End Sub
        End Sub

        Public Property CronoPanel As Border
            Get
                Return _cronoPanel
            End Get
            Set(value As Border)
                _cronoPanel = value
            End Set
        End Property

        Public ReadOnly Property MainWindow As MainWindow
            Get
                Return _mainWindow
            End Get
        End Property

    End Class
End Namespace
