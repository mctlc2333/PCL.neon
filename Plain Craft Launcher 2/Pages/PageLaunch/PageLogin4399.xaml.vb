Imports System.Windows.Media.Imaging

Public Class PageLogin4399
    Private IsFirstLoad As Boolean = True
    Private CaptchaId As String = ""
    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        '记住密码
        CheckRemember.Checked = Settings.Get(Of Boolean)("LoginRemember")
        If KeepInput AndAlso Not IsFirstLoad Then '避免第一次就以 KeepInput 的方式加载，导致文本框里没东西
            '保留输入，只刷新下拉框列表
            Dim Input As String = ComboName.Text
            ComboName.ItemsSource = If(Settings.Get(Of String)("Login4399Email") = "", Nothing, Settings.Get(Of String)("Login4399Email").ToString.Split("¨"))
            ComboName.Text = Input
        Else
            '不保留输入，刷新列表后自动选择第一项
            If Settings.Get(Of String)("Login4399Email") = "" Then
                ComboName.ItemsSource = Nothing
            Else
                ComboName.ItemsSource = Settings.Get(Of String)("Login4399Email").Split("¨")
                ComboName.Text = Settings.Get(Of String)("Login4399Email").BeforeFirst("¨")
                If Settings.Get(Of Boolean)("LoginRemember") Then TextPass.Password = Settings.Get(Of String)("Login4399Pass").BeforeFirst("¨").Trim
            End If
        End If
        '刷新验证码
        RefreshCaptcha()
        IsFirstLoad = False
    End Sub
    ''' <summary>
    ''' 刷新验证码图片。
    ''' </summary>
    Public Sub RefreshCaptcha()
        CaptchaId = ModNeteaseLogin.N4399CaptchaId()
        TextCaptcha.Text = ""
        RunInNewThread(
        Sub()
            Try
                Dim Bytes As Byte() = ModNeteaseLogin.N4399CaptchaImage(CaptchaId)
                Dim NewCaptchaId As String = CaptchaId
                RunInUi(
                Sub()
                    If NewCaptchaId = CaptchaId Then
                        ImgCaptcha.Source = BytesToBitmapImage(Bytes)
                    End If
                End Sub)
            Catch ex As Exception
                If Not ex.IsCanceled Then Logger.Info("4399 验证码获取失败：" & ex.GetDisplay(False))
            End Try
        End Sub, "Captcha Load")
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        If FrmLogin4399 Is Nothing Then
            Return New McLoginServer(McLoginType.Pt4399) With {.Token = "4399", .UserName = "", .Password = "", .Description = "4399", .Type = McLoginType.Pt4399, .BaseUrl = "4399"}
        Else
            Return New McLoginServer(McLoginType.Pt4399) With {.Token = "4399", .UserName = FrmLogin4399.ComboName.Text.Replace("¨", "").Trim, .Password = FrmLogin4399.TextPass.Password.Replace("¨", "").Trim, .Description = "4399", .Type = McLoginType.Pt4399, .BaseUrl = "4399", .Extra = FrmLogin4399.CaptchaId & "|" & FrmLogin4399.TextCaptcha.Text.Trim}
        End If
    End Function
    ''' <summary>
    ''' 当前页面的登录信息是否有效。
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginServer) As String
        If LoginData.UserName = "" Then Return "4399 用户名不能为空！"
        If LoginData.Password = "" Then Return "密码不能为空！"
        If LoginData.Extra = "" OrElse LoginData.Extra.BeforeFirst("|") = "" Then Return "验证码未加载，请稍候重试！"
        If LoginData.Extra.AfterFirst("|") = "" Then Return "请输入验证码！"
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

    '保存输入信息
    Private Sub ComboName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboName.TextChanged
        If sender.Text = "" Then TextPass.Password = ""
        If AniControlEnabled = 0 Then Settings.Set("Cache4399Access", "")  '迫使其不进行 Validate
    End Sub
    Private Sub TextPass_PasswordChanged(sender As Object, e As RoutedEventArgs) Handles TextPass.PasswordChanged
        If AniControlEnabled = 0 Then Settings.Set("Cache4399Access", "")
    End Sub
    Private Sub ComboName_SelectionChanged(sender As MyComboBox, e As SelectionChangedEventArgs) Handles ComboName.SelectionChanged
        If sender.SelectedIndex = -1 OrElse Not Settings.Get(Of Boolean)("LoginRemember") Then
            TextPass.Password = ""
        Else
            TextPass.Password = Settings.Get(Of String)("Login4399Pass").Split("¨")(sender.SelectedIndex).Trim
        End If
    End Sub
    Private Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckRemember.Change
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Checked)
    End Sub

    '验证码刷新
    Private Sub BtnCaptcha_Click(sender As Object, e As EventArgs) Handles BtnCaptcha.Click
        RefreshCaptcha()
    End Sub

    Private Function BytesToBitmapImage(Bytes As Byte()) As BitmapImage
        Using Mem As New MemoryStream(Bytes)
            Dim Image As New BitmapImage
            Image.BeginInit()
            Image.CacheOption = BitmapCacheOption.OnLoad
            Image.StreamSource = Mem
            Image.EndInit()
            Image.Freeze()
            Return Image
        End Using
    End Function

    '链接处理
    Private Sub Btn_Click(sender As Object, e As EventArgs) Handles BtnLink.Click
        OpenWebsite("https://www.4399.com/register.htm")
    End Sub

End Class
