Public Class PageLoginX19
    Private IsFirstLoad As Boolean = True
    ''' <summary>
    ''' 刷新页面显示的所有信息。
    ''' </summary>
    Public Sub Reload(KeepInput As Boolean)
        '记住密码
        CheckRemember.Checked = Settings.Get(Of Boolean)("LoginRemember")
        If KeepInput AndAlso Not IsFirstLoad Then '避免第一次就以 KeepInput 的方式加载，导致文本框里没东西
            '保留输入，只刷新下拉框列表
            Dim Input As String = ComboName.Text
            ComboName.ItemsSource = If(Settings.Get(Of String)("LoginX19Email") = "", Nothing, Settings.Get(Of String)("LoginX19Email").ToString.Split("¨"))
            ComboName.Text = Input
        Else
            '不保留输入，刷新列表后自动选择第一项
            If Settings.Get(Of String)("LoginX19Email") = "" Then
                ComboName.ItemsSource = Nothing
            Else
                ComboName.ItemsSource = Settings.Get(Of String)("LoginX19Email").Split("¨")
                ComboName.Text = Settings.Get(Of String)("LoginX19Email").BeforeFirst("¨")
                If Settings.Get(Of Boolean)("LoginRemember") Then TextPass.Password = Settings.Get(Of String)("LoginX19Pass").BeforeFirst("¨").Trim
            End If
        End If
        'Cookie 数据
        TextCookie.Text = Settings.Get(Of String)("LoginX19Cookie")
        IsFirstLoad = False
    End Sub
    ''' <summary>
    ''' 获取当前页面的登录信息。
    ''' 若未填写网易邮箱，则视为 Cookie 导入模式（Password 中为 Cookie JSON）。
    ''' </summary>
    Public Shared Function GetLoginData() As McLoginServer
        If FrmLoginX19 Is Nothing Then
            Return New McLoginServer(McLoginType.X19) With {.Token = "X19", .UserName = "", .Password = "", .Description = "网易中国版", .Type = McLoginType.X19, .BaseUrl = "x19"}
        Else
            Dim Email As String = FrmLoginX19.ComboName.Text.Replace("¨", "").Trim
            If Email = "" Then
                'Cookie 导入模式
                Return New McLoginServer(McLoginType.X19) With {.Token = "X19", .UserName = "", .Password = FrmLoginX19.TextCookie.Text.Replace("¨", "").Trim, .Description = "网易中国版", .Type = McLoginType.X19, .BaseUrl = "x19"}
            Else
                Return New McLoginServer(McLoginType.X19) With {.Token = "X19", .UserName = Email, .Password = FrmLoginX19.TextPass.Password.Replace("¨", "").Trim, .Description = "网易中国版", .Type = McLoginType.X19, .BaseUrl = "x19"}
            End If
        End If
    End Function
    ''' <summary>
    ''' 当前页面的登录信息是否有效。
    ''' </summary>
    Public Shared Function IsVaild(LoginData As McLoginServer) As String
        If LoginData.UserName = "" Then
            'Cookie 模式
            If LoginData.Password = "" Then Return "请填写网易邮箱和密码，或导入网易启动器的 Cookie 数据！"
        Else
            If LoginData.Password = "" Then Return "密码不能为空！"
        End If
        Return ""
    End Function
    Public Function IsVaild() As String
        Return IsVaild(GetLoginData())
    End Function

    '保存输入信息
    Private Sub ComboName_TextChanged(sender As Object, e As TextChangedEventArgs) Handles ComboName.TextChanged
        If sender.Text = "" Then TextPass.Password = ""
        If AniControlEnabled = 0 Then Settings.Set("CacheX19Access", "")  '迫使其不进行 Validate
    End Sub
    Private Sub TextPass_PasswordChanged(sender As Object, e As RoutedEventArgs) Handles TextPass.PasswordChanged
        If AniControlEnabled = 0 Then Settings.Set("CacheX19Access", "")
    End Sub
    Private Sub ComboName_SelectionChanged(sender As MyComboBox, e As SelectionChangedEventArgs) Handles ComboName.SelectionChanged
        If sender.SelectedIndex = -1 OrElse Not Settings.Get(Of Boolean)("LoginRemember") Then
            TextPass.Password = ""
        Else
            TextPass.Password = Settings.Get(Of String)("LoginX19Pass").Split("¨")(sender.SelectedIndex).Trim
        End If
    End Sub
    Private Sub CheckBoxChange(sender As MyCheckBox, e As Object) Handles CheckRemember.Change
        If AniControlEnabled = 0 Then Settings.Set(sender.Tag, sender.Checked)
    End Sub
    Private Sub TextCookie_TextChanged(sender As Object, e As TextChangedEventArgs) Handles TextCookie.TextChanged
        If AniControlEnabled = 0 Then
            Settings.Set("LoginX19Cookie", TextCookie.Text.Trim)
            Settings.Set("CacheX19Access", "")
        End If
    End Sub

    '链接处理
    Private Sub Btn_Click(sender As Object, e As EventArgs) Handles BtnLink.Click
        Settings.Set("LoginX19Cookie", "")
        TextCookie.Text = ""
        TextPass.Password = ""
    End Sub

End Class
