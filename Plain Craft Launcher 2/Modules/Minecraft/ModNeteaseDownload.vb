Imports System.IO
Imports System.Net
Imports System.Security.Cryptography
Imports System.Text

' =====================================================================
' ModNeteaseDownload：网易我的世界中国版（X19）客户端下载协议移植
' 协议来源：FantNel（https://github.com/fengheng1111/fantnel，GPL-3.0）
' 下载流程与 FantNel InstallerService.PrepareMinecraftClient / InstallGameMods 一致：
'   1. /game-patch-info mc_version=0 → base 基础包
'   2. /game-patch-info mc_version=<版本> → 版本包 + 核心库（core_lib）
'   3. 网络服 /game-auth-item-list/query/search-by-game（game_type=2）
'      租赁服 /game-auth-item-list/query/search-by-game（game_type=8）→ iid_list
'   4. /user-item-download-v2/get-list（需网易/4399 登录态签名）→ 白端基础模组
' 注：网易 CDN 文件虽以 .7z 命名，实际为 ZIP 格式（PK 头），可用 .NET 原生解压。
' =====================================================================
Public Module ModNeteaseDownload

    ' ============================ 端点 ============================
    Private Const UrlClient As String = "https://x19mclobt.nie.netease.com"
    Private Const UrlGateway As String = "https://x19apigatewayobt.nie.netease.com"
    Private Const UserAgent As String = "WPFLauncher/0.0.0.0"

    ' ============================ 实体 ============================
    Public Class CNPatchInfo
        Public Name As String = ""
        Public Url As String = ""
        Public Md5 As String = ""
        Public Size As Long = 0
        Public Version As String = ""
        Public McVersion As Long = 0
        Public CoreLibName As String = ""
        Public CoreLibUrl As String = ""
        Public CoreLibMd5 As String = ""
        Public CoreLibSize As Long = 0
    End Class

    Public Class CNModFile
        Public ItemId As String = ""
        Public MTypeId As String = ""
        Public EntityId As String = ""
        Public ResName As String = ""
        Public ResUrl As String = ""
        Public ResMd5 As String = ""
        Public ResSize As Long = 0
        Public JarMd5 As String = ""
        Public McVersionName As String = ""
    End Class

    ' ============================ 目录（与 FantNel PathUtil 同构，位于启动器目录 .game_cache） ============================
    Public Function CnCacheRoot() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".game_cache")
    End Function
    Public Function CnGameBase() As String
        Return Path.Combine(CnCacheRoot(), "Game", "Base")
    End Function
    Public Function CnGameBaseMc() As String
        Return Path.Combine(CnGameBase(), ".minecraft")
    End Function
    Public Function CnGameMods() As String
        Return Path.Combine(CnCacheRoot(), "GameMods")
    End Function

    ' ============================ 基础工具 ============================
    Private Function GetStr(ByVal Token As JToken, ByVal Name As String) As String
        If Token Is Nothing Then Return ""
        If Token(Name) Is Nothing Then Return ""
        Return Token(Name).ToString()
    End Function
    Private Function GetLong(ByVal Token As JToken, ByVal Name As String) As Long
        If Token Is Nothing Then Return 0
        If Token(Name) Is Nothing Then Return 0
        Try
            Return Token(Name).ToObject(Of Long)()
        Catch ex As Exception
            Return 0
        End Try
    End Function
    Public Function FormatSize(ByVal Size As Long) As String
        If Size >= 1024 * 1024 * 1024 Then
            Return (Size / 1024.0 / 1024.0 / 1024.0).ToString("0.00") & " GB"
        ElseIf Size >= 1024 * 1024 Then
            Return (Size / 1024.0 / 1024.0).ToString("0.00") & " MB"
        ElseIf Size >= 1024 Then
            Return (Size / 1024.0).ToString("0.0") & " KB"
        Else
            Return Size & " B"
        End If
    End Function

    Private Function Md5Hex(ByVal Text As String) As String
        Return Md5Hex(Encoding.UTF8.GetBytes(Text))
    End Function
    Private Function Md5Hex(ByVal Data As Byte()) As String
        Using Hasher As MD5 = MD5.Create()
            Dim Bytes As Byte() = Hasher.ComputeHash(Data)
            Dim Sb As New StringBuilder(Bytes.Length * 2)
            For Each B As Byte In Bytes
                Sb.Append(B.ToString("x2"))
            Next
            Return Sb.ToString()
        End Using
    End Function

    ' ============================ TokenUtil.Compute（与 FantNel TokenUtil 一致） ============================
    Public Function ComputeToken(ByVal Path As String, ByVal Body As String, ByVal UserId As String, ByVal UserToken As String) As Dictionary(Of String, String)
        If Not Path.StartsWith("/") Then Path = "/" & Path
        Dim Raw As Byte() = Encoding.UTF8.GetBytes(Md5Hex(UserToken) & Body & "0eGsBkhl" & Path)
        Dim Lower As String = Md5Hex(Raw)
        Dim BinaryStr As New StringBuilder(Lower.Length * 4)
        For Each C As Char In Lower
            BinaryStr.Append(Convert.ToString(Convert.ToInt32(C.ToString(), 16), 2).PadLeft(4, "0"c))
        Next
        Dim SecretBin As String = BinaryStr.ToString()
        SecretBin = SecretBin.Substring(6) & SecretBin.Substring(0, 6)
        Dim Tok As Byte() = Encoding.UTF8.GetBytes(Lower)
        Dim Blocks As Integer = SecretBin.Length \ 8
        For i = 0 To Blocks - 1
            Dim Block As String = SecretBin.Substring(i * 8, 8)
            Dim Num As Byte = 0
            For j = 0 To 7
                If Block.Chars(7 - j) = "1"c Then Num = Num Or CByte(1 << j)
            Next
            Tok(i) = Tok(i) Xor Num
        Next
        Dim S As String = Convert.ToBase64String(Tok, 0, 12) & "1"
        S = S.Replace("+"c, "m"c).Replace("/"c, "o"c)
        Return New Dictionary(Of String, String) From {{"user-id", UserId}, {"user-token", S}}
    End Function

    ' ============================ X19 JSON 请求（明文 body + 签名头，无缓存） ============================
    Private Function PostJson(ByVal Url As String, ByVal Body As String, ByVal UserId As String, ByVal UserToken As String,
                              Optional Timeout As Integer = 30000) As String
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Dim Request As HttpWebRequest = HttpWebRequest.Create(Url)
        Request.Method = "POST"
        Request.UserAgent = UserAgent
        Request.Timeout = Timeout
        Request.ReadWriteTimeout = Timeout
        Request.AllowAutoRedirect = True
        Request.ContentType = "application/json"
        Dim Headers As Dictionary(Of String, String) = ComputeToken(Url, Body, UserId, UserToken)
        For Each Pair In Headers
            Request.Headers(Pair.Key) = Pair.Value
        Next
        Dim BodyBytes As Byte() = Encoding.UTF8.GetBytes(Body)
        Request.ContentLength = BodyBytes.Length
        Using Stream = Request.GetRequestStream()
            Stream.Write(BodyBytes, 0, BodyBytes.Length)
        End Using
        Dim Response As HttpWebResponse = Nothing
        Try
            Response = Request.GetResponse()
            Using Stream = Response.GetResponseStream()
                If Stream Is Nothing Then Return ""
                Using Mem As New MemoryStream()
                    Stream.CopyTo(Mem)
                    Return Encoding.UTF8.GetString(Mem.ToArray())
                End Using
            End Using
        Catch ex As WebException
            Dim Status As Integer = -1
            If ex.Response IsNot Nothing Then Status = CInt(CType(ex.Response, HttpWebResponse).StatusCode)
            Throw New Exception("网易接口请求失败（" & Url & "），状态码 " & Status & "：" & ex.Message)
        Finally
            If Response IsNot Nothing Then Response.Close()
        End Try
    End Function

    Private Function CheckCode(ByVal Url As String, ByVal Text As String) As JObject
        Dim Root As JObject = Nothing
        Try
            Root = JObject.Parse(Text)
        Catch ex As Exception
            Throw New Exception("网易接口响应非 JSON（" & Url & "）：" & Text.LimitLen(200))
        End Try
        If Root("code") IsNot Nothing AndAlso Root("code").ToObject(Of Integer)() <> 0 Then
            Dim Msg As String = "未知错误"
            If Root("message") IsNot Nothing Then Msg = Root("message").ToString()
            Throw New Exception("网易接口返回错误（" & Url & "）：" & Msg)
        End If
        Return Root
    End Function

    ' ============================ 协议接口 ============================
    ''' <summary>
    ''' 获取客户端补丁信息。mc_version=0 返回基础包；指定版本返回该版本包 + 核心库。
    ''' </summary>
    Public Function GetGamePatchInfo(ByVal McVersion As Long) As CNPatchInfo
        Dim Json As New JObject From {{"mc_version", McVersion}}
        Dim Text As String = PostJson(UrlClient & "/game-patch-info", Json.ToString(Formatting.None), "", "")
        Dim Root As JObject = CheckCode(UrlClient & "/game-patch-info", Text)
        Dim Entity As JToken = Root("entity")
        If Entity Is Nothing Then Throw New Exception("获取网易中国版客户端信息失败：响应无数据")
        Return New CNPatchInfo With {
            .Name = GetStr(Entity, "name"), .Url = GetStr(Entity, "url"), .Md5 = GetStr(Entity, "md5"),
            .Size = GetLong(Entity, "size"), .Version = GetStr(Entity, "version"), .McVersion = GetLong(Entity, "mc_version"),
            .CoreLibName = GetStr(Entity, "core_lib_name"), .CoreLibUrl = GetStr(Entity, "core_lib_url"),
            .CoreLibMd5 = GetStr(Entity, "core_lib_md5"), .CoreLibSize = GetLong(Entity, "core_lib_size")}
    End Function

    ''' <summary>
    ''' 获取白端基础模组 ID 列表（网络服 GameType=2，租赁服 GameType=8）。
    ''' </summary>
    Public Function GetCoreModIidList(ByVal McVersion As Long, ByVal IsRental As Boolean) As List(Of String)
        Dim Json As New JObject From {{"mc_version_id", McVersion}, {"game_type", If(IsRental, 8L, 2L)}}
        Dim Text As String = PostJson(UrlGateway & "/game-auth-item-list/query/search-by-game", Json.ToString(Formatting.None), "", "")
        Dim Root As JObject = CheckCode(UrlGateway & "/game-auth-item-list/query/search-by-game", Text)
        Dim Result As New List(Of String)
        Dim Entity As JToken = Root("entity")
        If Entity Is Nothing Then Return Result
        Dim List As JToken = Entity("iid_list")
        If List Is Nothing Then Return Result
        For Each Item As JToken In List
            Result.Add(Item.ToString())
        Next
        Return Result
    End Function

    ''' <summary>
    ''' 获取白端基础模组下载信息（需网易/4399 登录态签名的 user-id/user-token）。
    ''' </summary>
    Public Function GetCoreModFiles(ByVal IidList As List(Of String), ByVal UserId As String, ByVal UserToken As String) As List(Of CNModFile)
        Dim ItemArray As New JArray()
        For Each Iid In IidList
            ItemArray.Add(Iid)
        Next
        Dim Json As New JObject From {{"item_id_list", ItemArray}}
        Dim Text As String = PostJson(UrlGateway & "/user-item-download-v2/get-list", Json.ToString(Formatting.None), UserId, UserToken)
        Dim Root As JObject = CheckCode(UrlGateway & "/user-item-download-v2/get-list", Text)
        Dim Result As New List(Of CNModFile)
        Dim Entity As JToken = Root("entity")
        If Entity Is Nothing Then Return Result
        For Each Item As JToken In Entity
            Dim ItemId As String = GetStr(Item, "item_id")
            Dim MTypeId As String = GetStr(Item, "mtypeid")
            Dim EntityId As String = GetStr(Item, "entity_id")
            Dim Subs As JToken = Item("sub_entities")
            If Subs Is Nothing Then Continue For
            For Each SubItem As JToken In Subs
                Dim F As New CNModFile
                F.ItemId = ItemId
                F.MTypeId = MTypeId
                F.EntityId = EntityId
                F.ResName = GetStr(SubItem, "res_name")
                F.ResUrl = GetStr(SubItem, "res_url")
                F.ResMd5 = GetStr(SubItem, "res_md5")
                F.ResSize = GetLong(SubItem, "res_size")
                F.JarMd5 = GetStr(SubItem, "jar_md5")
                F.McVersionName = GetStr(SubItem, "mc_version_name")
                If F.ResUrl <> "" Then Result.Add(F)
            Next
        Next
        Return Result
    End Function

    ' ============================ 下载 / 解压 ============================
    ''' <summary>
    ''' 流式下载文件到本地，支持进度回调（0-100）。
    ''' </summary>
    Public Sub DownloadFileStream(ByVal Url As String, ByVal DestPath As String, ByVal OnProgress As Action(Of Integer),
                                  Optional Timeout As Integer = 600000)
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Dim Request As HttpWebRequest = HttpWebRequest.Create(Url)
        Request.Method = "GET"
        Request.UserAgent = UserAgent
        Request.Timeout = Timeout
        Request.ReadWriteTimeout = Timeout
        Request.AllowAutoRedirect = True
        Using Response As HttpWebResponse = Request.GetResponse()
            Dim Total As Long = Response.ContentLength
            Using InStream = Response.GetResponseStream()
                Directory.CreateDirectory(Path.GetDirectoryName(DestPath))
                Using OutStream As New FileStream(DestPath, FileMode.Create, FileAccess.Write)
                    Dim Buffer(65535) As Byte
                    Dim Read As Integer = 0
                    Dim Written As Long = 0
                    Do
                        Read = InStream.Read(Buffer, 0, Buffer.Length)
                        If Read <= 0 Then Exit Do
                        OutStream.Write(Buffer, 0, Read)
                        Written += Read
                        If Total > 0 AndAlso OnProgress IsNot Nothing Then
                            Dim Percent As Integer = CInt(Written * 100 / Total)
                            If Percent > 100 Then Percent = 100
                            OnProgress(Percent)
                        End If
                    Loop
                End Using
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' 解压 zip（网易 CDN 包虽为 .7z 后缀，实际为 ZIP 格式）。
    ''' </summary>
    Public Sub ExtractZip(ByVal ArchivePath As String, ByVal DestDir As String)
        If Not Directory.Exists(DestDir) Then Directory.CreateDirectory(DestDir)
        System.IO.Compression.ZipFile.ExtractToDirectory(ArchivePath, DestDir)
    End Sub

    ''' <summary>
    ''' 安装核心库：jar 文件按相对路径合并到 .minecraft\libraries；非 jar 文件复制到 versions 版本目录。
    ''' </summary>
    Private Sub InstallCoreLibs(ByVal LibDir As String, ByVal McVersionName As String)
        If Not Directory.Exists(LibDir) Then Return
        Dim VersionsDir As String = Path.Combine(CnGameBaseMc(), "versions", McVersionName)
        Dim LibrariesDir As String = Path.Combine(CnGameBaseMc(), "libraries")
        Directory.CreateDirectory(LibrariesDir)
        For Each FilePath As String In Directory.GetFiles(LibDir, "*", SearchOption.AllDirectories)
            Dim FileName As String = Path.GetFileName(FilePath)
            If FileName.ToLowerInvariant().EndsWith(".jar") Then
                Dim Relative As String = FilePath.Substring(LibDir.Length).TrimStart("\"c, "/"c)
                Dim Target As String = Path.Combine(LibrariesDir, Relative)
                Directory.CreateDirectory(Path.GetDirectoryName(Target))
                File.Copy(FilePath, Target, True)
            Else
                Directory.CreateDirectory(VersionsDir)
                File.Copy(FilePath, Path.Combine(VersionsDir, FileName), True)
            End If
        Next
    End Sub

    ' ============================ 一键下载（网络服/租赁服） ============================
    ''' <summary>
    ''' 自动下载网易中国版客户端：base 基础包 → 版本包 → 核心库 → 白端基础模组（需登录）。
    ''' </summary>
    Public Sub InstallCNClient(ByVal McVersion As Long, ByVal McVersionName As String, ByVal IsRental As Boolean,
                               ByVal OnProgress As Action(Of Integer, String), ByVal IsCanceled As Func(Of Boolean))
        If IsCanceled() Then Exit Sub
        Report(OnProgress, 0, "正在获取网易中国版客户端信息…")
        Dim Base As CNPatchInfo = GetGamePatchInfo(0)
        If IsCanceled() Then Exit Sub
        Dim Ver As CNPatchInfo = GetGamePatchInfo(McVersion)
        Dim TypeName As String = If(IsRental, "租赁服", "网络服")
        Dim ShowVersion As String = If(Ver.Version <> "", Ver.Version, "未知")
        Report(OnProgress, 2, "已获取客户端信息：" & TypeName & " · 版本包 " & Ver.Name & "（补丁版本 " & ShowVersion & "）")

        ' 1. base 基础包
        Dim BaseZip As String = Path.Combine(CnCacheRoot(), "GameBase.7z")
        Report(OnProgress, 5, "正在下载基础包（" & FormatSize(Base.Size) & "）…")
        DownloadFileStream(Base.Url, BaseZip, Sub(p) Report(OnProgress, 5 + CInt(p * 15 / 100), "正在下载基础包… " & p & "%"))
        If IsCanceled() Then Exit Sub
        Report(OnProgress, 20, "正在解压基础包…")
        ExtractZip(BaseZip, CnGameBase())
        Try
            File.WriteAllText(Path.Combine(CnGameBase(), "GAME_BASE.MD5"), Base.Md5)
        Catch ex As Exception
        End Try
        Try : File.Delete(BaseZip) : Catch : End Try

        ' 2. 版本包
        Dim VerZip As String = Path.Combine(CnCacheRoot(), Ver.Version & ".7z")
        Report(OnProgress, 22, "正在下载版本包（" & FormatSize(Ver.Size) & "）…")
        DownloadFileStream(Ver.Url, VerZip, Sub(p) Report(OnProgress, 22 + CInt(p * 40 / 100), "正在下载版本包… " & p & "%"))
        If IsCanceled() Then Exit Sub
        Report(OnProgress, 62, "正在解压版本包…")
        ExtractZip(VerZip, CnGameBase())
        Try
            File.WriteAllText(Path.Combine(CnGameBase(), Ver.Version & ".MD5"), Ver.Md5)
        Catch ex As Exception
        End Try
        Try : File.Delete(VerZip) : Catch : End Try

        ' 3. 核心库
        If Ver.CoreLibUrl <> "" Then
            Dim LibZip As String = Path.Combine(CnCacheRoot(), Ver.Version & "_Lib.7z")
            Dim LibDir As String = Path.Combine(CnCacheRoot(), Ver.Version & "_libs")
            Report(OnProgress, 64, "正在下载核心库（" & FormatSize(Ver.CoreLibSize) & "）…")
            DownloadFileStream(Ver.CoreLibUrl, LibZip, Sub(p) Report(OnProgress, 64 + CInt(p * 15 / 100), "正在下载核心库… " & p & "%"))
            If IsCanceled() Then Exit Sub
            Report(OnProgress, 79, "正在解压核心库…")
            If Directory.Exists(LibDir) Then Directory.Delete(LibDir, True)
            ExtractZip(LibZip, LibDir)
            Try
                InstallCoreLibs(LibDir, McVersionName)
            Catch ex As Exception
                Report(OnProgress, 79, "核心库安装出现异常：" & ex.Message)
            End Try
            Try : File.Delete(LibZip) : Catch : End Try
        End If

        ' 4. 白端基础模组（需网易/4399 登录态）
        Dim UserId As String = Settings.Get(Of String)("CacheX19Uuid")
        Dim UserToken As String = Settings.Get(Of String)("CacheX19Access")
        If UserId <> "" AndAlso UserToken <> "" Then
            Report(OnProgress, 80, "正在获取" & TypeName & "基础模组列表…")
            Dim Iids As List(Of String) = Nothing
            Try
                Iids = GetCoreModIidList(McVersion, IsRental)
            Catch ex As Exception
                Iids = Nothing
            End Try
            If Iids IsNot Nothing AndAlso Iids.Count > 0 Then
                Try
                    Dim Files As List(Of CNModFile) = GetCoreModFiles(Iids, UserId, UserToken)
                    Dim Idx As Integer = 0
                    For Each F As CNModFile In Files
                        If IsCanceled() Then Exit Sub
                        Idx += 1
                        Report(OnProgress, 80 + CInt(Idx * 20 / Math.Max(Files.Count, 1)), "正在下载基础模组 " & Idx & "/" & Files.Count & "（" & F.ResName & "）…")
                        Dim ModRoot As String = Path.Combine(CnGameMods(), F.ItemId)
                        Directory.CreateDirectory(ModRoot)
                        Dim Archive As String = Path.Combine(ModRoot, F.ResName)
                        DownloadFileStream(F.ResUrl, Archive, Nothing)
                        Dim ExtractDir As String = Path.Combine(ModRoot, Path.GetFileNameWithoutExtension(F.ResName))
                        If Directory.Exists(ExtractDir) Then Directory.Delete(ExtractDir, True)
                        ExtractZip(Archive, ExtractDir)
                        Try : File.Delete(Archive) : Catch : End Try
                        Dim JarTarget As String = Path.Combine(ModRoot, Path.GetFileNameWithoutExtension(F.ResName) & "@" & F.MTypeId & "@" & F.EntityId & ".jar")
                        For Each J As String In Directory.GetFiles(ExtractDir, "*.jar", SearchOption.AllDirectories)
                            If File.Exists(JarTarget) Then Try : File.Delete(JarTarget) : Catch : End Try
                            File.Copy(J, JarTarget)
                            Exit For
                        Next
                        Try : Directory.Delete(ExtractDir, True) : Catch : End Try
                    Next
                Catch ex As Exception
                    Report(OnProgress, 80, "基础模组下载失败（可能未登录网易/4399）：" & ex.Message)
                End Try
            End If
        Else
            Report(OnProgress, 80, "未检测到网易/4399 登录态（下载基础模组需要），已跳过模组，仅下载客户端本体。")
        End If

        Report(OnProgress, 100, "下载完成！客户端文件位于：" & CnGameBaseMc() & vbCrLf & "可在启动页使用网易中国版/4399 账号登录后游玩。")
    End Sub

    Private Sub Report(ByVal OnProgress As Action(Of Integer, String), ByVal Percent As Integer, ByVal Message As String)
        If OnProgress IsNot Nothing Then OnProgress(Percent, Message)
    End Sub

End Module
