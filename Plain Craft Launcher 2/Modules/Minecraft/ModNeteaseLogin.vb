Imports System.IO
Imports System.Net
Imports System.Runtime.CompilerServices
Imports System.Security.Cryptography
Imports System.Text

' =====================================================================
' ModNeteaseLogin：网易我的世界中国版（X19）登录协议移植
' 协议来源：FantNel（https://github.com/fengheng1111/fantnel，GPL-3.0）
' 覆盖：4399 密码登录（ptlogin）、4399 OAuth（4399com）、网易邮箱直登（MPay/mkey）、
'       网易 Cookie 导入，统一经 x19 login-otp + authentication-otp 换取网易 session。
' 本模块不依赖 PCL 的网络缓存客户端，使用独立 HttpWebRequest（避免动态接口被缓存污染）。
' =====================================================================
Public Module ModNeteaseLogin

    ' ============================ 端点 ============================
    Private Const UrlGateway As String = "https://x19apigatewayobt.nie.netease.com"
    Private Const UrlCore As String = "https://x19obtcore.nie.netease.com:8443"
    Private Const UrlUpdate As String = "https://x19.update.netease.com"
    Private Const UrlMgbSdk As String = "https://mgbsdk.matrix.netease.com"
    Private Const UrlPt4399 As String = "https://ptlogin.4399.com"
    Private Const Url4399Micro As String = "https://microgame.5054399.net"
    Private Const Url4399OAuth As String = "https://m.4399api.com"
    Private Const UrlMkey As String = "https://service.mkey.163.com"

    Private Const MkeyGameId As String = "aecfrxodyqaaaajp-g-x19"
    Private Const UserAgent As String = "WPFLauncher/0.0.0.0"

    ' ============================ 随机数 ============================
    Private Rand As New Random()

    ' ============================ X19 基础 HTTP（无缓存） ============================

    ''' <summary>
    ''' 发送 HttpWebRequest 并读取响应字节。若 StatusCode 非 2xx 抛异常。
    ''' </summary>
    Private Function X19Request(Url As String, Optional Method As String = "GET", Optional BodyBytes As Byte() = Nothing,
                                Optional ContentType As String = Nothing, Optional ExtraHeaders As Dictionary(Of String, String) = Nothing,
                                Optional Cookies As CookieContainer = Nothing, Optional Timeout As Integer = 30000) As Byte()
        ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol Or SecurityProtocolType.Tls12
        Dim Request As HttpWebRequest = HttpWebRequest.Create(Url)
        Request.Method = Method
        Request.UserAgent = UserAgent
        Request.Timeout = Timeout
        Request.ReadWriteTimeout = Timeout
        Request.AllowAutoRedirect = True
        Request.CookieContainer = If(Cookies, New CookieContainer)
        If ContentType IsNot Nothing Then Request.ContentType = ContentType
        If ExtraHeaders IsNot Nothing Then
            For Each Pair In ExtraHeaders
                Request.Headers(Pair.Key) = Pair.Value
            Next
        End If
        If BodyBytes IsNot Nothing Then
            Request.ContentLength = BodyBytes.Length
            Using Stream = Request.GetRequestStream()
                Stream.Write(BodyBytes, 0, BodyBytes.Length)
            End Using
        End If
        Dim Response As HttpWebResponse = Nothing
        Try
            Response = Request.GetResponse()
            Using Stream = Response.GetResponseStream()
                If Stream Is Nothing Then Return New Byte() {}
                Using Mem As New MemoryStream()
                    Stream.CopyTo(Mem)
                    Return Mem.ToArray()
                End Using
            End Using
        Catch ex As WebException
            Dim Status As Integer = -1
            If ex.Response IsNot Nothing Then Status = CInt(CType(ex.Response, HttpWebResponse).StatusCode)
            Throw New Exception("网络请求失败（" & Method & "，" & Url & "），状态码 " & Status & "：" & ex.Message)
        Finally
            If Response IsNot Nothing Then Response.Close()
        End Try
    End Function

    Private Function X19GetText(Url As String, Optional Cookies As CookieContainer = Nothing) As String
        Return Encoding.UTF8.GetString(X19Request(Url, "GET", Nothing, Nothing, Nothing, Cookies))
    End Function

    Private Function X19PostText(Url As String, Body As String, Optional ContentType As String = "application/x-www-form-urlencoded",
                                 Optional Cookies As CookieContainer = Nothing, Optional Referer As String = Nothing) As String
        Dim Headers As Dictionary(Of String, String) = Nothing
        If Referer IsNot Nothing Then
            Headers = New Dictionary(Of String, String) From {{"Referer", Referer}}
        End If
        Return Encoding.UTF8.GetString(X19Request(Url, "POST", Encoding.UTF8.GetBytes(Body), ContentType, Headers, Cookies))
    End Function

    Private Function X19PostRaw(Url As String, Body As Byte(), Optional ContentType As String = "application/octet-stream") As Byte()
        Return X19Request(Url, "POST", Body, ContentType)
    End Function

    ' ============================ 工具函数 ============================

    Private Function Utf8(ByVal S As String) As Byte()
        Return Encoding.UTF8.GetBytes(S)
    End Function

    Private Function Str(ByVal B As Byte()) As String
        Return Encoding.UTF8.GetString(B)
    End Function

    Private Function Md5Hex(ByVal S As String) As String
        Using Md5 As MD5 = MD5.Create()
            Dim Bytes As Byte() = Md5.ComputeHash(Utf8(S))
            Dim Sb As New StringBuilder(32)
            For Each B In Bytes
                Sb.Append(B.ToString("x2"))
            Next
            Return Sb.ToString()
        End Using
    End Function

    Private Function HexToLower(ByVal B As Byte()) As String
        Dim Sb As New StringBuilder(B.Length * 2)
        For Each Bt In B
            Sb.Append(Bt.ToString("x2"))
        Next
        Return Sb.ToString()
    End Function

    Private Function DecodeHex(ByVal S As String) As Byte()
        If S.Length Mod 2 <> 0 Then Throw New Exception("无效的 HEX 字符串")
        Dim Result(S.Length \ 2 - 1) As Byte
        For i = 0 To Result.Length - 1
            Result(i) = Convert.ToByte(S.Substring(i * 2, 2), 16)
        Next
        Return Result
    End Function

    Private Function Base64Encode(ByVal S As String) As String
        Return Convert.ToBase64String(Utf8(S))
    End Function

    Private Function UrlEncode(ByVal S As String) As String
        Return WebUtility.UrlEncode(S)
    End Function

    Private Function AesEncryptEcbPkcs7(ByVal Data As Byte(), ByVal Key As Byte()) As Byte()
        Using Aes As Aes = Aes.Create()
            Aes.Key = Key
            Aes.Mode = CipherMode.ECB
            Aes.Padding = PaddingMode.PKCS7
            Using Encryptor = Aes.CreateEncryptor()
                Return Encryptor.TransformFinalBlock(Data, 0, Data.Length)
            End Using
        End Using
    End Function

    Private Function AesCbcNoPaddingEncrypt(ByVal Data As Byte(), ByVal Key As Byte(), ByVal Iv As Byte()) As Byte()
        Using Aes As Aes = Aes.Create()
            Aes.Key = Key
            Aes.IV = Iv
            Aes.Mode = CipherMode.CBC
            Aes.Padding = PaddingMode.None
            Using Encryptor = Aes.CreateEncryptor()
                Return Encryptor.TransformFinalBlock(Data, 0, Data.Length)
            End Using
        End Using
    End Function

    Private Function AesCbcNoPaddingDecrypt(ByVal Data As Byte(), ByVal Key As Byte(), ByVal Iv As Byte()) As Byte()
        Using Aes As Aes = Aes.Create()
            Aes.Key = Key
            Aes.IV = Iv
            Aes.Mode = CipherMode.CBC
            Aes.Padding = PaddingMode.None
            Using Decryptor = Aes.CreateDecryptor()
                Return Decryptor.TransformFinalBlock(Data, 0, Data.Length)
            End Using
        End Using
    End Function

    ' ============================ X19 HttpEncrypt / HttpDecrypt ============================

    Private ReadOnly HttpKeys As Byte()() = (From K In "MK6mipwmOUedplb6,OtEylfId6dyhrfdn,VNbhn5mvUaQaeOo9,bIEoQGQYjKd02U0J,fuaJrPwaH2cfXXLP,LEkdyiroouKQ4XN1,jM1h27H4UROu427W,DhReQada7gZybTDk,ZGXfpSTYUvcdKqdY,AZwKf7MWZrJpGR5W,amuvbcHw38TcSyPU,SI4QotspbjhyFdT0,VP4dhjKnDGlSJtbB,UXDZx4KhZywQ2tcn,NIK73ZNvNqzva4kd,WeiW7qU766Q1YQZI".Split(","c)).Select(Function(S) Encoding.GetEncoding("us-ascii").GetBytes(S)).ToArray()

    Private Function HttpEncrypt(ByVal BodyIn As Byte()) As Byte()
        Dim Iv As Byte() = Encoding.GetEncoding("us-ascii").GetBytes("szkgpbyimxavqjcn")
        ' 原明文 + 16 字节 IV 副本作为填充，使总长成为 16 的倍数
        Dim PaddedLen As Integer = CInt(Math.Ceiling((BodyIn.Length + 16) / 16.0)) * 16
        Dim Padded(PaddedLen - 1) As Byte
        Array.Copy(BodyIn, Padded, BodyIn.Length)
        Array.Copy(Iv, 0, Padded, BodyIn.Length, 16)
        Dim KeyIndex As Byte = CByte((Rand.Next(0, 15) << 4) Or 2)
        Dim Encrypted As Byte() = AesCbcNoPaddingEncrypt(Padded, HttpKeys((KeyIndex >> 4) And &HF), Iv)
        Dim Result(16 + Encrypted.Length) As Byte
        Array.Copy(Iv, Result, 16)
        Array.Copy(Encrypted, 0, Result, 16, Encrypted.Length)
        Result(Result.Length - 1) = KeyIndex
        Return Result
    End Function

    Private Function HttpDecrypt(ByVal Body As Byte()) As Byte()
        If Body.Length < &H12 Then Return Nothing
        Dim Iv(15) As Byte
        Array.Copy(Body, Iv, 16)
        Dim EncryptedLen As Integer = Body.Length - 1 - 16
        Dim Encrypted(EncryptedLen - 1) As Byte
        Array.Copy(Body, 16, Encrypted, 0, EncryptedLen)
        Dim Key As Byte() = HttpKeys((Body(Body.Length - 1) >> 4) And &HF)
        Dim Decrypted As Byte() = AesCbcNoPaddingDecrypt(Encrypted, Key, Iv)
        Dim Scissor As Integer = 0
        Dim ScissorPos As Integer = Decrypted.Length - 1
        While Scissor < 16
            If Decrypted(ScissorPos) <> 0 Then Scissor += 1
            ScissorPos -= 1
        End While
        Dim Result(ScissorPos) As Byte
        Array.Copy(Decrypted, Result, ScissorPos + 1)
        Return Result
    End Function

    ' ============================ X19 版本号 ============================

    Private _NideGameVersion As String = ""
    ''' <summary>
    ''' 最新盒子版本号（从 x19 patchlist 反推，缓存到内存）。
    ''' </summary>
    Public Function NideGameVersion() As String
        If _NideGameVersion <> "" Then Return _NideGameVersion
        Dim Content As String = X19GetText(UrlUpdate & "/pl/x19_java_patchlist")
        If Content = "" Then Throw New Exception("获取网易中国版版本失败（patchlist 为空）")
        Const SizeMarker As String = """:{""size"":"
        Dim Pos As Integer = Content.LastIndexOf(SizeMarker)
        If Pos = -1 Then Throw New Exception("获取网易中国版版本失败（patchlist 无版本信息）")
        Content = Content.Substring(0, Pos)
        Pos = Content.LastIndexOf(""""c)
        If Pos = -1 Then Throw New Exception("获取网易中国版版本失败（patchlist 解析失败）")
        _NideGameVersion = Content.Substring(Pos + 1)
        If _NideGameVersion = "" Then Throw New Exception("获取网易中国版版本失败（版本号为空）")
        Return _NideGameVersion
    End Function

    ' ============================ SAuth / uni_sauth（MgbSdk） ============================

    ''' <summary>
    ''' 生成 SAuth 令牌 JSON（与 FantNel MgbSdk.GenerateSAuth 一致，PascalCase 字段）。
    ''' </summary>
    Public Function GenerateSAuth(SdkUid As String, SessionId As String, Channel As String, Platform As String, UserId As String, Timestamp As String) As String
        Dim Str As String = Guid.NewGuid().ToString("N")
        Dim Json As New JObject From {
            {"AppChannel", Channel},
            {"ClientLoginSn", Str},
            {"DeviceId", Str},
            {"GameId", "x19"},
            {"LoginChannel", Channel},
            {"SdkUid", SdkUid},
            {"SessionId", SessionId},
            {"Timestamp", Timestamp},
            {"Platform", Platform},
            {"SourcePlatform", Platform},
            {"Udid", Str},
            {"UserId", UserId}
        }
        Return Json.ToString(Formatting.None)
    End Function

    ''' <summary>
    ''' 校验 SAuth 并换取网易侧会话。code=="200" 即通过。
    ''' </summary>
    Private Sub AuthSession(ByVal SAuthJson As String)
        Dim ResponseText As String = X19PostText(UrlMgbSdk & "/x19/sdk/uni_sauth", SAuthJson, "application/json")
        Dim Token As JToken = Nothing
        Try
            Token = JObject.Parse(ResponseText)
        Catch ex As Exception
            Throw New Exception("网易 SDK 校验失败（响应非 JSON）：" & ResponseText.LimitLen(200))
        End Try
        If Token("code") IsNot Nothing AndAlso Token("code").ToString() = "200" Then Return
        Dim Msg As String = ""
        If Token("msg") IsNot Nothing Then
            Msg = Token("msg").ToString()
        ElseIf Token("status") IsNot Nothing Then
            Msg = Token("status").ToString()
        End If
        Throw New Exception("网易 SDK 校验失败：" & Msg)
    End Sub

    ' ============================ X19 登录（login-otp → authentication-otp） ============================

    ''' <summary>
    ''' 使用 Cookie/SAuth JSON 登录网易中国版，返回认证信息（EntityAuthenticationOtp）。
    ''' </summary>
    Public Function NideLoginByAuthJson(AuthJson As String, Optional Channel As String = "") As X19LoginResult
        ' 非网易渠道（4399 等）先做 uni_sauth 校验；网易渠道（login_channel=netease）跳过
        Dim LoginChannel As String = Channel
        If LoginChannel = "" Then
            Dim CheckJson As JObject = Nothing
            Try
                CheckJson = JObject.Parse(AuthJson)
            Catch ex As Exception
            End Try
            If CheckJson IsNot Nothing AndAlso CheckJson("login_channel") IsNot Nothing Then LoginChannel = CheckJson("login_channel").ToString()
        End If
        If LoginChannel <> "netease" Then
            AuthSession(AuthJson)
        End If
        ' 第一步：login-otp
        Dim OtpJson As JObject = Nothing
        Dim LoginOtpText As String = ""
        Try
            Dim Req As New JObject From {{"sauth_json", AuthJson}}
            LoginOtpText = X19PostText(UrlCore & "/login-otp", Req.ToString(Formatting.None), "application/json")
            OtpJson = JObject.Parse(LoginOtpText)
        Catch ex As Exception
            Throw New Exception("登录 OTP 获取失败（" & ex.Message & "）" & vbCrLf & LoginOtpText.LimitLen(300))
        End Try
        Dim Code As Integer = -1
        If OtpJson("code") IsNot Nothing Then Code = OtpJson("code").ToObject(Of Integer)()
        If Code <> 0 Then
            Dim Msg As String = If(OtpJson("message") IsNot Nothing, OtpJson("message").ToString(), "未知错误")
            Throw New Exception("登录失败：" & Msg)
        End If
        Dim Data As JToken = OtpJson("entity")
        If Data Is Nothing Then Throw New Exception("登录失败：OTP 响应无数据")
        Dim Aid As String = If(Data("aid") IsNot Nothing, Data("aid").ToString(), "")
        Dim OtpToken As String = If(Data("otp_token") IsNot Nothing, Data("otp_token").ToString(), "")
        If Aid = "" OrElse OtpToken = "" Then Throw New Exception("登录失败：OTP 响应数据不完整")
        ' 第二步：authentication-otp（AES 加密）
        Dim Version As String = NideGameVersion()
        Dim Upper As String = GenerateHexStringUpper(4)
        Dim Detail As New JObject From {
            {"os_name", "windows"},
            {"os_ver", "Microsoft Windows 11 专业版"},
            {"mac_addr", GenerateRandomMacAddress()},
            {"udid", "0000000000000000" & Upper},
            {"app_ver", Version},
            {"sdk_ver", ""},
            {"network", ""},
            {"disk", Upper},
            {"is64bit", "1"},
            {"video_card1", "Microsoft Hyper-V 视频"},
            {"video_card2", "Microsoft Remote Display Adapter"},
            {"video_card3", ""},
            {"video_card4", ""},
            {"launcher_type", "PC_java"},
            {"pay_channel", GetPayChannel(AuthJson)},
            {"dotnet_ver", "4.8.0"},
            {"cpu_type", "Intel(R) Core(TM) i7-9700 CPU @ 3.00GHz"},
            {"ram_size", "16384"},
            {"device_width", "1920"},
            {"device_height", "1080"},
            {"os_detail", "10.0.26100"}
        }
        Dim AuthData As New JObject From {
            {"sa_data", Detail.ToString(Formatting.None)},
            {"sauth_json", AuthJson},
            {"version", New JObject From {{"version", Version}, {"launcher_md5", ""}, {"updater_md5", ""}}},
            {"sdkuid", Nothing},
            {"aid", Aid},
            {"hasMessage", False},
            {"hasGmail", False},
            {"otp_token", OtpToken},
            {"otp_pwd", Nothing},
            {"lock_time", 0},
            {"env", Nothing},
            {"min_engine_version", Nothing},
            {"min_patch_version", Nothing},
            {"verify_status", 0},
            {"unisdk_login_json", Nothing},
            {"token", Nothing},
            {"is_register", True},
            {"entity_id", Nothing}
        }
        Dim AuthBytes As Byte() = HttpEncrypt(Utf8(AuthData.ToString(Formatting.None)))
        Dim ResponseBytes As Byte() = X19PostRaw(UrlCore & "/authentication-otp", AuthBytes, "application/octet-stream")
        Dim Decrypted As Byte() = HttpDecrypt(ResponseBytes)
        If Decrypted Is Nothing Then Throw New Exception("登录失败：authentication-otp 响应解密失败")
        Dim AuthJson2 As JObject = Nothing
        Try
            AuthJson2 = JObject.Parse(Str(Decrypted))
        Catch ex As Exception
            Throw New Exception("登录失败：authentication-otp 响应解析失败：" & Str(Decrypted).LimitLen(300))
        End Try
        Dim AuthCode As Integer = -1
        If AuthJson2("code") IsNot Nothing Then AuthCode = AuthJson2("code").ToObject(Of Integer)()
        If AuthCode <> 0 Then
            Throw New Exception("登录失败：" & If(AuthJson2("message") IsNot Nothing, AuthJson2("message").ToString(), "未知错误"))
        End If
        Dim AuthData2 As JToken = AuthJson2("entity")
        If AuthData2 Is Nothing Then Throw New Exception("登录失败：认证响应无数据")
        Return New X19LoginResult With {
            .EntityId = GetStr(AuthData2, "entity_id"),
            .Account = GetStr(AuthData2, "account"),
            .Token = GetStr(AuthData2, "token"),
            .Sead = GetStr(AuthData2, "sead"),
            .Aid = GetStr(AuthData2, "aid"),
            .SdkUid = GetStr(AuthData2, "sdkuid"),
            .AccessToken = GetStr(AuthData2, "access_token"),
            .VerifyStatus = If(AuthData2("verify_status") IsNot Nothing, AuthData2("verify_status").ToObject(Of Integer)(), 0)
        }
    End Function

    Private Function GetStr(ByVal Token As JToken, ByVal Name As String) As String
        If Token Is Nothing Then Return ""
        If Token(Name) Is Nothing Then Return ""
        Return Token(Name).ToString()
    End Function

    Private Function GetPayChannel(ByVal AuthJson As String) As String
        Try
            Dim Json As JObject = JObject.Parse(AuthJson)
            If Json("app_channel") IsNot Nothing Then Return Json("app_channel").ToString()
            If Json("AppChannel") IsNot Nothing Then Return Json("AppChannel").ToString()
        Catch ex As Exception
        End Try
        Return "netease"
    End Function

    Private Function GenerateHexStringUpper(ByVal Length As Integer) As String
        Dim NumArray(Length - 1) As Byte
        Rand.NextBytes(NumArray)
        Dim Sb As New StringBuilder(Length * 2)
        For Each B In NumArray
            Sb.Append(B.ToString("X2"))
        Next
        Return Sb.ToString()
    End Function

    Private Function GenerateRandomMacAddress() As String
        Dim Mac(5) As Byte
        Rand.NextBytes(Mac)
        Mac(0) = CByte(Mac(0) And &HFE)
        Mac(0) = CByte(Mac(0) Or &H02)
        Dim Parts As New List(Of String)
        For Each B In Mac
            Parts.Add(B.ToString("X2"))
        Next
        Return String.Join(":", Parts)
    End Function

    ' ============================ 4399 密码登录（N4399 / ptlogin） ============================

    ''' <summary>
    ''' 4399 主站账号密码登录。返回 SAuth JSON（供 NideLoginByAuthJson 使用）。
    ''' </summary>
    Public Function Pt4399Login(Username As String, Password As String, CaptchaId As String, Captcha As String) As String
        Dim Cookies As New CookieContainer
        ' 构建登录参数
        Dim Params As New Dictionary(Of String, String)
        Params("postLoginHandler") = "default"
        Params("layoutSelfAdapting") = "true"
        Params("externalLogin") = "qq"
        Params("displayMode") = "popup"
        Params("layout") = "vertical"
        Params("bizId") = "2201001794"
        Params("appId") = "kid_wdsj"
        Params("gameId") = "wd"
        Params("redirectUrl") = ""
        Params("sessionId") = CaptchaId
        Params("mainDivId") = "popup_login_div"
        Params("includeFcmInfo") = "false"
        Params("level") = "8"
        Params("regLevel") = "8"
        Params("userNameLabel") = "4399用户名"
        Params("userNameTip") = "请输入4399用户名"
        Params("welcomeTip") = "欢迎回到4399"
        Params("sec") = "1"
        Params("iframeId") = "popup_login_frame"
        Params("autoLogin") = "on"
        Params("username") = Username
        Params("password") = Password
        Params("inputCaptcha") = Captcha
        Dim LoginText As String = X19PostText(UrlPt4399 & "/ptlogin/login.do?v=1", BuildForm(Params),
                                              "application/x-www-form-urlencoded", Cookies, "http://ptlogin.4399.com/ptlogin/loginFrame.do")
        Dim ErrText As String = Extract4399ErrorTip(LoginText)
        If ErrText.Length > 0 Then Throw New Exception(ErrText)
        ' 检查登录状态（跟随重定向）
        Dim UnixTime As Long = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        Dim LoginQuery As String = "v=2018_11_26_16&postLoginHandler=redirect&checkLoginUserCookie=true&redirectUrl=" &
            UrlEncode("https://cdn.h5wan.4399sj.com/microterminal-h5-frame?game_id=500352&rand_time=" & UnixTime)
        Dim UcenterQuery As String = "action=login&appId=kid_wdsj&loginLevel=8&regLevel=8&bizId=2201001794&externalLogin=qq&qrLogin=true&layout=vertical&level=101&css=" &
            UrlEncode("https://microgame.5054399.net/v2/resource/cssSdk/default/login.css?" & LoginQuery)
        Dim CheckQuery As String = "appId=kid_wdsj&gameUrl=" & UrlEncode("https://cdn.h5wan.4399sj.com/microterminal-h5-frame?game_id=500352") &
            "&isCrossDomain=1&nick=null&onLineStart=false&ptLogin=true&rand_time=$randTime&retUrl=" &
            UrlEncode("https://ptlogin.4399.com/resource/ucenter.html?" & UcenterQuery) & "&show=1"
        Dim FinalUri As String = ""
        Dim CheckUrl2 As String = UrlPt4399 & "/ptlogin/checkKidLoginUserCookie.do?" & CheckQuery
        Dim Request As HttpWebRequest = HttpWebRequest.Create(CheckUrl2)
        Request.UserAgent = UserAgent
        Request.AllowAutoRedirect = True
        Request.CookieContainer = Cookies
        Dim Response2 As HttpWebResponse = Nothing
        Try
            Response2 = Request.GetResponse()
            FinalUri = Response2.ResponseUri.ToString()
        Catch ex As WebException
            Throw New Exception("4399 登录状态检查失败：" & ex.Message)
        Finally
            If Response2 IsNot Nothing Then Response2.Close()
        End Try
        Dim QueryParams As Dictionary(Of String, String) = ParseQuery(FinalUri)
        If Not QueryParams.ContainsKey("sig") Then Throw New Exception("4399 登录状态检查失败（缺少 sig）")
        ' 获取统一认证信息
        Dim UniAuth As Dictionary(Of String, String) = Get4399UniAuth(QueryParams, Cookies)
        ' 生成 SAuth
        Return GenerateSAuth(UniAuth("uid"), UniAuth("token"), "4399pc", "pc", If(UniAuth.ContainsKey("username"), UniAuth("username"), ""), If(UniAuth.ContainsKey("time"), UniAuth("time"), ""))
    End Function

    Private Function Get4399UniAuth(ByVal QueryParams As Dictionary(Of String, String), ByVal Cookies As CookieContainer) As Dictionary(Of String, String)
        Dim QueryStr As String = "game_id=500352&nick=null&sig=" & UrlEncode(QueryParams("sig")) & "&uid=" & UrlEncode(QueryParams("uid")) &
            "&fcm=0&isCrossDomain=1&show=1&rand_time=$randTime&ptusertype=4399&ptLogin=true&time=" & UrlEncode(QueryParams("time")) &
            "&validateState=" & UrlEncode(QueryParams("validateState")) & "&username=" & UrlEncode(QueryParams("username"))
        Dim Url As String = Url4399Micro & "/v2/service/sdk/info?_=" & QueryParams("time") & "&queryStr=" & UrlEncode(QueryStr)
        Dim ResponseText As String = X19GetText(Url, Cookies)
        Dim UniAuth As JObject = Nothing
        Try
            UniAuth = JObject.Parse(ResponseText)
        Catch ex As Exception
            Throw New Exception("4399 统一认证解析失败：" & ResponseText.LimitLen(200))
        End Try
        If UniAuth("data") Is Nothing OrElse UniAuth("data")("sdkLoginData") Is Nothing Then
            Throw New Exception("4399 统一认证解析失败（无 sdkLoginData）：" & ResponseText.LimitLen(200))
        End If
        Dim LoginData As JToken = UniAuth("data")("sdkLoginData")
        Dim Result As New Dictionary(Of String, String)
        For Each Key In {"uid", "token", "time", "username"}
            If LoginData(Key) IsNot Nothing Then Result(Key) = LoginData(Key).ToString()
        Next
        If Not Result.ContainsKey("uid") OrElse Not Result.ContainsKey("token") Then
            Throw New Exception("4399 统一认证数据不完整：" & ResponseText.LimitLen(200))
        End If
        Return Result
    End Function

    Private Function Extract4399ErrorTip(ByVal Html As String) As String
        If Not Html.Contains("<html>") Then Return Html.Trim()
        Const StartMarker As String = "login_err_tip"">"
        Const EndMarker As String = "</div>"
        Dim StartIndex As Integer = Html.IndexOf(StartMarker, StringComparison.Ordinal)
        If StartIndex = -1 Then Return ""
        StartIndex += StartMarker.Length
        Dim EndIndex As Integer = Html.IndexOf(EndMarker, StartIndex, StringComparison.Ordinal)
        If EndIndex = -1 Then Return ""
        Return Html.Substring(StartIndex, EndIndex - StartIndex).Trim()
    End Function

    ' ============================ 4399 OAuth 登录（NCom4399 / 4399com） ============================

    ''' <summary>
    ''' 4399 OAuth2（m.4399api.com）账号登录。返回 SAuth JSON。
    ''' </summary>
    Public Function Com4399Login(Username As String, Password As String, CaptchaId As String, Captcha As String) As String
        Dim Cookies As New CookieContainer
        Dim OAuthText As String = X19GetText(Url4399OAuth & "/openapi/oauth-callback.html?gamekey=44770&game_key=115716", Cookies)
        Dim OAuthCallback As JObject = Nothing
        Try
            OAuthCallback = JObject.Parse(OAuthText)
        Catch ex As Exception
            Throw New Exception("4399 OAuth 初始化失败：" & OAuthText.LimitLen(200))
        End Try
        Dim ResultUrl As String = If(OAuthCallback("result") IsNot Nothing, OAuthCallback("result").ToString(), "")
        If ResultUrl = "" Then Throw New Exception("4399 OAuth 初始化失败（无 result）")
        Dim QueryParams As Dictionary(Of String, String) = ParseQuery(ResultUrl)
        If Not QueryParams.ContainsKey("client_id") OrElse Not QueryParams.ContainsKey("state") Then
            Throw New Exception("4399 OAuth 初始化失败（缺少 client_id/state）")
        End If
        Dim ClientId As String = QueryParams("client_id")
        Dim State As String = QueryParams("state")
        Dim Ref1 As String = If(QueryParams.ContainsKey("ref"), QueryParams("ref"), "")
        Dim Params As New Dictionary(Of String, String)
        Params("isInputRealname") = "false"
        Params("isVaildRealname") = "false"
        Params("sec") = "0"
        Params("client_id") = ClientId
        Params("state") = State
        Params("ref") = Ref1
        Params("response_type") = "TOKEN"
        Params("scope") = "basic"
        Params("bizId") = "2100001792"
        Params("auth_action") = "ORILOGIN"
        Params("redirect_uri") = "https://m.4399api.com/openapi/oauth-callback.html?gamekey=44770&game_key=115716"
        Params("username") = Username
        Params("password") = Password
        Params("captcha_id") = CaptchaId
        Params("captcha") = Captcha
        Dim LoginText As String = X19PostText(UrlPt4399 & "/oauth2/loginAndAuthorize.do?channel=&sdk=op&sdk_version=3.18.0.682",
                                              BuildForm(Params), "application/x-www-form-urlencoded", Cookies, "https://ptlogin.4399.com/oauth2/authorize.do")
        Dim ErrText As String = Extract4399OAuthErrorTip(LoginText)
        If ErrText.Length > 0 Then Throw New Exception(ErrText)
        Dim UserInfo As JObject = Nothing
        Try
            UserInfo = JObject.Parse(LoginText)
        Catch ex As Exception
            Throw New Exception("4399 OAuth 登录响应解析失败：" & LoginText.LimitLen(200))
        End Try
        If UserInfo("code") Is Nothing OrElse UserInfo("code").ToString() <> "100" Then
            Throw New Exception(If(UserInfo("message") IsNot Nothing, UserInfo("message").ToString(), "4399 OAuth 登录失败"))
        End If
        Dim UserResult As JToken = UserInfo("result")
        If UserResult Is Nothing Then Throw New Exception("4399 OAuth 登录失败（无 result）")
        Dim Uid As String = If(UserResult("uid") IsNot Nothing, UserResult("uid").ToString(), "")
        Dim SessionState As String = If(UserResult("state") IsNot Nothing, UserResult("state").ToString(), "")
        If Uid = "" OrElse SessionState = "" Then Throw New Exception("4399 OAuth 登录失败（uid/state 为空）")
        Return GenerateSAuth(Uid, SessionState, "4399com", "ad", "", "")
    End Function

    Private Function Extract4399OAuthErrorTip(ByVal Html As String) As String
        Const StartMarker As String = "login_err_msg"">"
        Const EndMarker As String = "</p>"
        Dim StartIndex As Integer = Html.IndexOf(StartMarker, StringComparison.Ordinal)
        If StartIndex = -1 Then Return ""
        StartIndex += StartMarker.Length
        Dim EndIndex As Integer = Html.IndexOf(EndMarker, StartIndex, StringComparison.Ordinal)
        If EndIndex = -1 Then Return ""
        Return Html.Substring(StartIndex, EndIndex - StartIndex).Trim()
    End Function

    ' ============================ 网易邮箱直登（MPay / mkey） ============================

    Private _MkeyUnique As String = ""
    Private _MkeyDeviceId As String = ""
    Private _MkeyDeviceKey As String = ""

    ''' <summary>
    ''' 网易邮箱密码直登，返回 EntityX19Cookie JSON。
    ''' </summary>
    Public Function MkeyLogin(Email As String, Password As String) As String
        If _MkeyUnique = "" Then
            _MkeyUnique = Guid.NewGuid().ToString("N")
        End If
        If _MkeyDeviceId = "" Then
            CreateMkeyDevice()
        End If
        ' params = AES(json{password:md5, unique_id, username}, deviceKey).hex
        Dim UserParams As New JObject From {
            {"password", Md5Hex(Password)},
            {"unique_id", _MkeyUnique},
            {"username", Email}
        }
        Dim ParamsEncrypted As String = HexToLower(AesEncryptEcbPkcs7(Utf8(UserParams.ToString(Formatting.None)), DecodeHex(_MkeyDeviceKey)))
        Dim BaseParams As New Dictionary(Of String, String)
        AddMkeyBaseParams(BaseParams)
        BaseParams("opt_fields") = "nickname,avatar,realname_status,mobile_bind_status,mask_related_mobile,related_login_status"
        BaseParams("params") = ParamsEncrypted
        BaseParams("un") = Base64Encode(Email)
        Dim ResponseText As String = X19PostText(UrlMkey & "/mpay/games/" & MkeyGameId & "/devices/" & _MkeyDeviceId & "/users",
                                                 BuildForm(BaseParams), "application/x-www-form-urlencoded")
        Dim UserResponse As JObject = Nothing
        Try
            UserResponse = JObject.Parse(ResponseText)
        Catch ex As Exception
            Throw New Exception("网易通行证登录失败：" & ResponseText.LimitLen(300))
        End Try
        If UserResponse("user") Is Nothing Then
            Dim Reason As String = If(UserResponse("reason") IsNot Nothing, UserResponse("reason").ToString(), ResponseText)
            Throw New Exception("网易通行证登录失败：" & Reason)
        End If
        Dim User As JToken = UserResponse("user")
        Dim UserId As String = If(User("id") IsNot Nothing, User("id").ToString(), "")
        Dim UserToken As String = If(User("token") IsNot Nothing, User("token").ToString(), "")
        If UserId = "" OrElse UserToken = "" Then Throw New Exception("网易通行证登录失败（user 数据不完整）")
        Return BuildX19Cookie(UserId, UserToken, _MkeyDeviceId)
    End Function

    Private Sub CreateMkeyDevice()
        Dim BaseParams As New Dictionary(Of String, String)
        AddMkeyBaseParams(BaseParams)
        BaseParams("unique_id") = _MkeyUnique
        BaseParams("brand") = "Microsoft"
        BaseParams("device_model") = "pc_mode"
        BaseParams("device_name") = "PC-" & GenerateRandomString(12)
        BaseParams("device_type") = "Computer"
        BaseParams("init_urs_device") = "0"
        BaseParams("mac") = GenerateRandomMacAddress()
        BaseParams("resolution") = "1920x1080"
        BaseParams("system_name") = "windows"
        BaseParams("system_version") = "10.0.22621"
        Dim ResponseText As String = X19PostText(UrlMkey & "/mpay/games/" & MkeyGameId & "/devices",
                                                 BuildForm(BaseParams), "application/x-www-form-urlencoded")
        Dim DeviceResponse As JObject = Nothing
        Try
            DeviceResponse = JObject.Parse(ResponseText)
        Catch ex As Exception
            Throw New Exception("网易设备注册失败：" & ResponseText.LimitLen(300))
        End Try
        If DeviceResponse("device") Is Nothing Then Throw New Exception("网易设备注册失败（无 device）")
        _MkeyDeviceId = If(DeviceResponse("device")("id") IsNot Nothing, DeviceResponse("device")("id").ToString(), "")
        _MkeyDeviceKey = If(DeviceResponse("device")("key") IsNot Nothing, DeviceResponse("device")("key").ToString(), "")
        If _MkeyDeviceId = "" OrElse _MkeyDeviceKey = "" Then Throw New Exception("网易设备注册失败（数据不完整）")
    End Sub

    Private Sub AddMkeyBaseParams(ByRef Params As Dictionary(Of String, String))
        Params("app_channel") = "netease"
        Params("app_mode") = "2"
        Params("app_type") = "games"
        Params("arch") = "win_x64"
        Params("cv") = "c4.2.0"
        Params("mcount_app_key") = "EEkEEXLymcNjM42yLY3Bn6AO15aGy4yq"
        Params("mcount_transaction_id") = "0"
        Params("process_id") = Process.GetCurrentProcess().Id.ToString()
        Params("sv") = "10.0.22621"
        Params("updater_cv") = "c1.0.0"
        Params("game_id") = MkeyGameId
        Params("gv") = NideGameVersion()
    End Sub

    ''' <summary>
    ''' 构建网易 X19 Cookie JSON（login_channel=netease）。
    ''' </summary>
    Public Function BuildX19Cookie(SdkUid As String, SessionId As String, DeviceId As String) As String
        Dim Json As New JObject From {
            {"gameid", "x19"},
            {"login_channel", "netease"},
            {"app_channel", "netease"},
            {"platform", "pc"},
            {"sdkuid", SdkUid},
            {"sessionid", SessionId},
            {"sdk_version", "4.2.0"},
            {"udid", Guid.NewGuid().ToString("N").ToUpper()},
            {"deviceid", DeviceId},
            {"aim_info", "{""aim"":""127.0.0.1"",""country"":""CN"",""tz"":""+0800"",""tzid"":""""}"}
        }
        Return Json.ToString(Formatting.None)
    End Function

    Private Function GenerateRandomString(ByVal Length As Integer) As String
        Const Chars As String = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789"
        Dim Sb As New StringBuilder(Length)
        For i = 0 To Length - 1
            Sb.Append(Chars.Chars(Rand.Next(Chars.Length)))
        Next
        Return Sb.ToString()
    End Function

    ' ============================ 4399 验证码 ============================

    ''' <summary>
    ''' 生成 4399 验证码 ID（3 个 GUID 拼接，与 FantNel 一致）。
    ''' </summary>
    Public Function N4399CaptchaId() As String
        Return Guid.NewGuid().ToString("N") & Guid.NewGuid().ToString("N") & Guid.NewGuid().ToString("N")
    End Function

    ''' <summary>
    ''' 获取 4399 验证码图片（PNG 字节）。
    ''' </summary>
    Public Function N4399CaptchaImage(CaptchaId As String) As Byte()
        Return X19Request(UrlPt4399 & "/ptlogin/captcha.do?captchaId=" & CaptchaId)
    End Function

    ' ============================ 通用工具 ============================

    Private Function BuildForm(ByVal Params As Dictionary(Of String, String)) As String
        Dim Parts As New List(Of String)
        For Each Pair In Params
            Parts.Add(Pair.Key & "=" & UrlEncode(Pair.Value))
        Next
        Return String.Join("&", Parts)
    End Function

    Private Function ParseQuery(ByVal Url As String) As Dictionary(Of String, String)
        Dim Result As New Dictionary(Of String, String)
        Dim QIndex As Integer = Url.IndexOf("?"c)
        If QIndex = -1 Then Return Result
        Dim Query As String = Url.Substring(QIndex + 1)
        For Each Part In Query.Split("&"c)
            If Part = "" Then Continue For
            Dim Eq As Integer = Part.IndexOf("="c)
            If Eq = -1 Then
                Result(WebUtility.UrlDecode(Part)) = ""
            Else
                Result(WebUtility.UrlDecode(Part.Substring(0, Eq))) = WebUtility.UrlDecode(Part.Substring(Eq + 1))
            End If
        Next
        Return Result
    End Function

    ''' <summary>
    ''' 截取字符串前 N 个字符（用于报错信息）。
    ''' </summary>
    <Extension>
    Public Function LimitLen(ByVal S As String, ByVal Max As Integer) As String
        If S Is Nothing Then Return ""
        If S.Length <= Max Then Return S
        Return S.Substring(0, Max) & "..."
    End Function

    ' ============================ 登录结果 ============================

    Public Class X19LoginResult
        Public EntityId As String = ""
        Public Account As String = ""
        Public Token As String = ""
        Public Sead As String = ""
        Public Aid As String = ""
        Public SdkUid As String = ""
        Public AccessToken As String = ""
        Public VerifyStatus As Integer = 0
    End Class

End Module
