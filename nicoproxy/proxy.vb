Imports TrotiNet
Imports Newtonsoft

Public Class Proxy
    Inherits TrotiNet.ProxyLogic

    Shared URLTable As New Dictionary(Of String, String)
    Shared Directory As String = "[path to save (don't need last backslash)]"

    Dim videoid As String
    Dim act As Actions = Actions.None

    Dim full As String = Nothing
    Dim full_part As String = Nothing
    Dim low As String = Nothing
    Dim low_part As String = Nothing
    Dim isEconomy As Boolean = False

    Enum Actions
        None
        GetFlv
        Watch
        Video
        VideoResume
    End Enum


    Public Overloads Shared Function CreateProxy(ByVal cs As HttpSocket) As Proxy
        Return New Proxy(cs)
    End Function

    Sub New(ByVal clientsocket As HttpSocket)
        MyBase.New(clientsocket)
    End Sub

    Function CutWrappedText(ByVal leftb As String, ByVal rightb As String, ByVal str As String) As String
        Dim lpos = str.IndexOf(leftb)
        Dim substr = str.Substring(lpos + leftb.Length)
        Dim rp = substr.IndexOf(rightb)
        Return If(rp <> -1, substr.Substring(0, rp), substr)
    End Function

    Sub SendFile(ByVal path As String, Optional addlength As Integer = 0)
        Dim fs As New IO.FileStream(path, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite)
        SocketBP.WriteAsciiLine("HTTP/1.1 200 OK")
        SocketBP.WriteAsciiLine("Content-Length: " & fs.Length + addlength)
        Dim extp = path.LastIndexOf(".")
        Dim ext = path.Substring(extp + 1)
        Dim ct = ""
        Select Case ext
            Case "mp4"
                ct = "video/mp4"
            Case "flv"
                ct = "video/x-flv"
            Case "swf"
                ct = "application/x-shockwave-flash"
        End Select
        SocketBP.WriteAsciiLine("Content-Type: " + ct)
        SocketBP.WriteAsciiLine("")

        Dim written As Integer = 0
        Dim buffer(1023) As Byte
        While written < fs.Length
            Dim len = fs.Read(buffer, 0, buffer.Length)
            Try
                SocketBP.WriteBinary(buffer, len)
            Catch e As System.Net.Sockets.SocketException
                Exit While
            End Try
            written = written + len
        End While
        fs.Close()
    End Sub

    Protected Overrides Sub OnReceiveRequest()
        ' keep-alive 対策
        videoid = ""
        full = Nothing
        full_part = Nothing
        low = Nothing
        low_part = Nothing
        isEconomy = False
        act = Actions.None

        If RequestLine.URI.Contains("flapi.nicovideo.jp/api/getflv") Then
            act = Actions.GetFlv
            Dim getflv = "flapi.nicovideo.jp/api/getflv"
            Dim p = RequestLine.URI.IndexOf(getflv)
            Dim substr = RequestLine.URI.Substring(p + getflv.Length)
            Select Case substr(0)
                Case "/"
                    Dim questionpos = substr.IndexOf("?")
                    If questionpos <> -1 Then
                        videoid = substr.Substring(1, questionpos - 1)
                    Else
                        videoid = substr.Substring(1)
                    End If
                Case "?"
                    Dim vpos = substr.IndexOf("v=")
                    Dim vstr = substr.Substring(vpos + 2)
                    Dim andpos = vstr.IndexOf("&")
                    If andpos <> -1 Then
                        videoid = vstr.Substring(0, andpos)
                    Else
                        videoid = vstr
                    End If
            End Select
        ElseIf RequestLine.URI.Contains("www.nicovideo.jp/watch/")
            act = Actions.Watch
            videoid = CutWrappedText("www.nicovideo.jp/watch/", "?", RequestLine.URI)
        ElseIf RequestLine.URI.Contains("nicovideo.jp/") AndAlso Not RequestLine.URI.Contains("www") Then
            For Each e In URLTable
                If e.Value = RequestLine.URI Then
                    videoid = e.Key
                    act = Actions.Video
                    Exit For
                End If
            Next

            ' ビデオの取得である場合
            If act = Actions.Video Then
                isEconomy = RequestLine.URI.Contains("low")
                ' 既に存在するか
                Dim files = My.Computer.FileSystem.GetFiles(Directory)
                Dim videos = (From e In files Where e.Contains(videoid + "_")).ToArray()

                For Each e In videos
                    If e.Contains("full") Then
                        If e.Contains("part") Then
                            full_part = e
                        Else
                            full = e
                        End If
                    ElseIf e.Contains("low")
                        If e.Contains("part") Then
                            low_part = e
                        Else
                            low = e
                        End If
                    End If
                Next

                ' fullが存在すればそれを返す
                If full IsNot Nothing Then
                    SendFile(full)
                    State.NextStep = AddressOf AbortRequest
                ElseIf low IsNot Nothing AndAlso isEconomy Then
                    ' lowが存在していてlowを要求していればlowを返す
                    SendFile(low)
                    State.NextStep = AddressOf AbortRequest
                Else
                    ' 送るべきものが存在しないとき
                    If isEconomy Then
                        If low_part IsNot Nothing Then
                            RequestHeaders.Range = "bytes=" & My.Computer.FileSystem.GetFileInfo(low_part).Length & "-"
                            act = Actions.VideoResume
                        End If
                    Else
                        If full_part IsNot Nothing Then
                            RequestHeaders.Range = "bytes=" & My.Computer.FileSystem.GetFileInfo(full_part).Length & "-"
                            act = Actions.VideoResume
                        End If
                    End If
                End If
            End If
        End If
        MyBase.OnReceiveRequest()
    End Sub

    Sub AddDictionaryFromGetFlv(ByVal getflvstr As String)
        Dim splitted = getflvstr.Split("&")
        For Each e In splitted
            Dim kv = e.Split("=")
            Dim k = kv(0)
            If k <> "url" Then
                Continue For
            End If
            Dim v = System.Web.HttpUtility.UrlDecode(kv(1))
            URLTable(videoid) = v
            Exit For
        Next
    End Sub

    Sub AddDictionaryFromWatch(ByVal watch As String)
        Dim magic1 As String = "<div id=""watchAPIDataContainer"" style=""display:none"">"
        Dim magic2 As String = "</div>"
        Dim datacontainer As String = CutWrappedText(magic1, magic2, watch)
        Dim jsontext As String = Web.HttpUtility.HtmlDecode(datacontainer)
        Dim jobj = Json.Linq.JObject.Parse(jsontext)
        Dim flashvars = jobj.Item("flashvars")
        Dim flvinfo = Web.HttpUtility.UrlDecode(flashvars.Item("flvInfo").ToString())
        AddDictionaryFromGetFlv(flvinfo)
    End Sub

    Function DownloadFile(ByVal path As String, ByVal length As Integer) As Boolean
        Dim contentlength As Integer = length
        Dim read As Integer = 0
        'SocketPS.TunnelDataTo(SocketBP)

        Dim filestrm As New IO.FileStream(path, IO.FileMode.Append, IO.FileAccess.Write, IO.FileShare.ReadWrite)
        While read < contentlength
            Dim len = SocketPS.ReadBinary()
            Try
                SocketBP.WriteBinary(SocketPS.Buffer, len)
            Catch e As System.Net.Sockets.SocketException
                Exit While
            End Try
            filestrm.Write(SocketPS.Buffer, 0, len)
            read = read + len
        End While
        filestrm.Close()
        Return read = contentlength
    End Function

    Function GetExtension(ByVal mimetype As String) As String
        Select Case mimetype
            Case "video/mp4"
                Return "mp4"
            Case "application/x-shockwave-flash"
                Return "swf"
            Case "video/flv"
                Return "flv"
            Case Else
                Return "bin"
        End Select
    End Function

    Protected Overrides Sub OnReceiveResponse()
        Select Case act
            Case Actions.GetFlv
                Dim content = GetContent()
                Dim str As String = System.Text.Encoding.ASCII.GetString(content)
                SendResponseStatusAndHeaders()
                SocketBP.WriteBinary(content)
                AddDictionaryFromGetFlv(str)
            Case Actions.Watch
                Dim content = GetContent()
                Dim str As String
                If ResponseHeaders.ContentEncoding = "gzip" Then
                    Dim strm As New IO.MemoryStream(content)
                    Dim gzipstrm As New IO.Compression.GZipStream(strm, IO.Compression.CompressionMode.Decompress)
                    Dim sr As New IO.StreamReader(gzipstrm, System.Text.Encoding.UTF8)
                    str = sr.ReadToEnd()
                Else
                    str = System.Text.Encoding.UTF8.GetString(content)
                End If
                SendResponseStatusAndHeaders()
                SocketBP.WriteBinary(content)
                AddDictionaryFromWatch(str)
            Case Actions.Video
                ' 存在しなければダウンロード
                Dim ext = GetExtension(ResponseHeaders.Headers("content-type"))

                Dim pathtosave = String.Format("{0}\{1}_{2}.{3}", Directory, videoid, If(isEconomy, "low", "full"), ext)
                SendResponseStatusAndHeaders()
                Dim complete = DownloadFile(pathtosave, ResponseHeaders.ContentLength)

                If Not complete Then
                    My.Computer.FileSystem.MoveFile(pathtosave, pathtosave + ".part")
                End If
            Case Actions.VideoResume
                Dim len = ResponseHeaders.ContentLength
                Dim ext = GetExtension(ResponseHeaders.Headers("content-type"))
                Dim pathtosave = String.Format("{0}\{1}_{2}.{3}", Directory, videoid, If(isEconomy, "low", "full"), ext)

                If isEconomy Then
                    SendFile(low_part, len)
                    My.Computer.FileSystem.MoveFile(low_part, low_part.Substring(0, low_part.Length - 5))
                Else
                    SendFile(full_part, len)
                    My.Computer.FileSystem.MoveFile(full_part, full_part.Substring(0, full_part.Length - 5))
                End If

                Dim complete = DownloadFile(pathtosave, len)
                If Not complete Then
                    My.Computer.FileSystem.MoveFile(pathtosave, pathtosave + ".part")
                End If
            Case Else
                MyBase.OnReceiveResponse()
        End Select
    End Sub

End Class
