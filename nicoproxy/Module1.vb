Module Module1

    Sub Main()

        Const port As Integer = 12345
        Const enableIPv6 As Boolean = False

        Dim s As New TrotiNet.TcpServer(port, enableIPv6)
        s.Start(AddressOf Proxy.CreateProxy)
        s.InitListenFinished.WaitOne()
        If s.InitListenException IsNot Nothing Then
            Throw s.InitListenException
        End If
        While True
            System.Threading.Thread.Sleep(1000)
        End While

    End Sub

End Module
