Imports System.Windows.Input
Imports System.IO
Imports FFMpegCore

Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.

    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        MyBase.OnStartup(e)
        GlobalFFOptions.Configure(New FFOptions With {
            .BinaryFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg")
        })
        EventManager.RegisterClassHandler(GetType(Window), Window.PreviewKeyDownEvent, New KeyEventHandler(AddressOf GlobalWindow_PreviewKeyDown), True)
    End Sub

    Private Sub GlobalWindow_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        Dim mainWindow = TryCast(Current.MainWindow, MainWindow)
        If mainWindow Is Nothing Then Return

        If mainWindow.HandleGlobalPreviewKeyDown(e) Then
            e.Handled = True
        End If
    End Sub

End Class
