Imports System.Runtime.InteropServices

''' <summary>
''' Coloca o próprio aplicativo num Job Object do Windows com "matar ao fechar".
''' Todo processo que o app iniciar depois disso (ffmpeg.exe e ffprobe.exe, inclusive os que o
''' FFMpegCore abre por dentro) entra automaticamente no mesmo Job. Assim:
'''  - quando o app fecha (ou trava e é encerrado), o Windows mata todos esses processos na hora;
'''  - não sobram ffmpeg órfãos consumindo CPU depois de fechar o programa.
''' </summary>
Public NotInheritable Class ProcessosFilhos
    Private Sub New()
    End Sub

    ' Mantido aberto durante toda a vida do app: fechar este handle é o que dispara a limpeza
    Private Shared _job As IntPtr = IntPtr.Zero

    Private Const JobObjectExtendedLimitInformation As Integer = 9
    Private Const JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE As UInteger = &H2000UI

    <StructLayout(LayoutKind.Sequential)>
    Private Structure JOBOBJECT_BASIC_LIMIT_INFORMATION
        Public PerProcessUserTimeLimit As Long
        Public PerJobUserTimeLimit As Long
        Public LimitFlags As UInteger
        Public MinimumWorkingSetSize As UIntPtr
        Public MaximumWorkingSetSize As UIntPtr
        Public ActiveProcessLimit As UInteger
        Public Affinity As UIntPtr
        Public PriorityClass As UInteger
        Public SchedulingClass As UInteger
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure IO_COUNTERS
        Public ReadOperationCount As ULong
        Public WriteOperationCount As ULong
        Public OtherOperationCount As ULong
        Public ReadTransferCount As ULong
        Public WriteTransferCount As ULong
        Public OtherTransferCount As ULong
    End Structure

    <StructLayout(LayoutKind.Sequential)>
    Private Structure JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        Public BasicLimitInformation As JOBOBJECT_BASIC_LIMIT_INFORMATION
        Public IoInfo As IO_COUNTERS
        Public ProcessMemoryLimit As UIntPtr
        Public JobMemoryLimit As UIntPtr
        Public PeakProcessMemoryUsed As UIntPtr
        Public PeakJobMemoryUsed As UIntPtr
    End Structure

    <DllImport("kernel32.dll", CharSet:=CharSet.Unicode, SetLastError:=True)>
    Private Shared Function CreateJobObject(lpJobAttributes As IntPtr, lpName As String) As IntPtr
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function SetInformationJobObject(hJob As IntPtr, infoClass As Integer, lpInfo As IntPtr, cbInfoLength As UInteger) As Boolean
    End Function

    <DllImport("kernel32.dll", SetLastError:=True)>
    Private Shared Function AssignProcessToJobObject(hJob As IntPtr, hProcess As IntPtr) As Boolean
    End Function

    ''' <summary>
    ''' Cria o Job e coloca o app dentro dele. Chamar uma vez, no início do programa.
    ''' Se falhar, o app segue normalmente (só perde a limpeza automática).
    ''' </summary>
    Public Shared Function Iniciar() As Boolean
        If _job <> IntPtr.Zero Then Return True
        Try
            Dim job = CreateJobObject(IntPtr.Zero, Nothing)
            If job = IntPtr.Zero Then Return False

            Dim info As New JOBOBJECT_EXTENDED_LIMIT_INFORMATION()
            info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE

            Dim tamanho = Marshal.SizeOf(GetType(JOBOBJECT_EXTENDED_LIMIT_INFORMATION))
            Dim ptr = Marshal.AllocHGlobal(tamanho)
            Try
                Marshal.StructureToPtr(info, ptr, False)
                If Not SetInformationJobObject(job, JobObjectExtendedLimitInformation, ptr, CUInt(tamanho)) Then Return False
            Finally
                Marshal.FreeHGlobal(ptr)
            End Try

            ' O próprio app entra no Job; os filhos que ele criar herdam a participação
            If Not AssignProcessToJobObject(job, Process.GetCurrentProcess().Handle) Then
                Debug.WriteLine($"Não foi possível colocar o app no Job Object (erro {Marshal.GetLastWin32Error()}).")
                Return False
            End If

            _job = job   ' nunca fechado de propósito: o Windows fecha ao encerrar o app e mata os filhos
            Return True
        Catch ex As Exception
            Debug.WriteLine($"Erro ao criar o Job Object: {ex.Message}")
            Return False
        End Try
    End Function
End Class
