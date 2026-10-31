'ModPlugins.vb —— 分支新增：插件加载模块
'实现说明：
' 1. 扫描 exe 同目录 plugins\ 文件夹下的 *.dll，作为插件加载。
' 2. 插件采用『约定 + 反射』的鸭子类型接口，无需引用启动器程序集即可开发：
'    - 入口类：名称以 Plugin 结尾或为 Main，且具有无参构造函数；
'    - 可选属性：Name / Version / Author / Description（String）；
'    - 可选方法：OnLoad()（加载时调用）、OnUnload()（卸载时调用，暂未使用）、
'      GetPage()（返回一个 FrameworkElement 作为插件页面）。
' 3. 单个插件加载失败不会影响启动器与其它插件。

Imports System.IO
Imports System.Reflection
Imports System.Windows

''' <summary>
''' 单个插件的运行时信息。
''' </summary>
Public Class PclPluginInfo

    ''' <summary>
    ''' 插件 DLL 的完整路径。
    ''' </summary>
    Public FilePath As String = ""

    ''' <summary>
    ''' 插件入口实例。
    ''' </summary>
    Public Instance As Object = Nothing

    ''' <summary>
    ''' 插件名称（约定属性 Name，缺省显示文件名）。
    ''' </summary>
    Public Name As String = ""

    ''' <summary>
    ''' 插件版本（约定属性 Version）。
    ''' </summary>
    Public Version As String = ""

    ''' <summary>
    ''' 插件作者（约定属性 Author）。
    ''' </summary>
    Public Author As String = ""

    ''' <summary>
    ''' 插件描述（约定属性 Description）。
    ''' </summary>
    Public Description As String = ""

    ''' <summary>
    ''' 是否成功加载（含 OnLoad 成功）。
    ''' </summary>
    Public Loaded As Boolean = False

    ''' <summary>
    ''' 加载/运行错误信息（加载失败或运行时出错时记录）。
    ''' </summary>
    Public ErrorMessage As String = ""

End Class

Public Module ModPlugins

    ''' <summary>
    ''' 已扫描到的插件列表。
    ''' </summary>
    Public PluginList As New List(Of PclPluginInfo)

    Private HasScanned As Boolean = False

    ''' <summary>
    ''' 插件文件夹路径（exe 同目录 plugins）。
    ''' </summary>
    Public Function PluginsDir() As String
        Return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins")
    End Function

    ''' <summary>
    ''' 扫描插件文件夹并加载全部插件。
    ''' </summary>
    Public Sub ScanPlugins()
        PluginList.Clear()
        HasScanned = True

        '确保插件文件夹存在（不存在也无需报错）
        Try
            If Not Directory.Exists(PluginsDir()) Then Directory.CreateDirectory(PluginsDir())
        Catch ex As Exception
            Logger.Warn("创建插件文件夹失败：" & ex.Message)
        End Try

        Dim Files As String() = {}
        Try
            Files = Directory.GetFiles(PluginsDir(), "*.dll")
        Catch ex As Exception
            Logger.Warn("读取插件文件夹失败：" & ex.Message)
        End Try

        For Each File As String In Files
            Dim Info As New PclPluginInfo With {.FilePath = File}
            Try
                Dim Assembly As Assembly = Assembly.LoadFrom(File)
                '跳过启动器自身的程序集
                If Assembly.GetName().Name.IndexOf("Plain Craft Launcher", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    Info.ErrorMessage = "忽略启动器自身程序集"
                    PluginList.Add(Info)
                    Continue For
                End If

                Dim EntryFound As Boolean = False
                For Each Type As Type In Assembly.GetTypes()
                    If Not Type.IsClass Then Continue For
                    If Type.IsAbstract Then Continue For
                    '约定：入口类名以 Plugin 结尾，或为 Main，且必须有无参构造函数
                    If Not (Type.Name.EndsWith("Plugin") OrElse Type.Name = "Main") Then Continue For
                    If Type.GetConstructor(Type.EmptyTypes) Is Nothing Then Continue For

                    Dim Instance As Object = Activator.CreateInstance(Type)
                    Info.Instance = Instance
                    Info.Name = ReadProperty(Instance, "Name", "")
                    Info.Version = ReadProperty(Instance, "Version", "")
                    Info.Author = ReadProperty(Instance, "Author", "")
                    Info.Description = ReadProperty(Instance, "Description", "")

                    '调用 OnLoad（失败只记录，不阻塞其它插件）
                    Try
                        InvokeMethod(Instance, "OnLoad")
                    Catch ex As Exception
                        Info.ErrorMessage = "OnLoad 调用失败：" & ex.Message
                    End Try

                    Info.Loaded = True
                    EntryFound = True
                    Exit For
                Next

                If Not EntryFound Then
                    Info.ErrorMessage = "未找到插件入口类（类名需以 Plugin 结尾或为 Main，且需有无参构造函数）"
                End If
            Catch ex As Exception
                Info.ErrorMessage = "加载失败：" & ex.Message
            End Try
            PluginList.Add(Info)
        Next
    End Sub

    ''' <summary>
    ''' 仅首次调用时扫描（供插件页打开时使用，避免重复扫描）。
    ''' </summary>
    Public Sub ScanPluginsIfNeeded()
        If Not HasScanned Then ScanPlugins()
    End Sub

    ''' <summary>
    ''' 获取插件页面（约定方法 GetPage，返回 FrameworkElement；失败返回 Nothing）。
    ''' </summary>
    Public Function GetPluginPage(Info As PclPluginInfo) As FrameworkElement
        If Info Is Nothing OrElse Info.Instance Is Nothing Then Return Nothing
        Try
            Dim Method As MethodInfo = Info.Instance.GetType().GetMethod("GetPage")
            If Method Is Nothing Then Return Nothing
            Dim Result As Object = Method.Invoke(Info.Instance, Nothing)
            Return TryCast(Result, FrameworkElement)
        Catch ex As Exception
            Info.ErrorMessage = "获取插件页面失败：" & ex.Message
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' 读取约定的字符串属性，失败或缺省时返回默认值。
    ''' </summary>
    Private Function ReadProperty(Instance As Object, Name As String, DefaultValue As String) As String
        Try
            Dim PropertyInfo As PropertyInfo = Instance.GetType().GetProperty(Name)
            If PropertyInfo IsNot Nothing AndAlso PropertyInfo.CanRead Then
                Dim Value As Object = PropertyInfo.GetValue(Instance, Nothing)
                If Value IsNot Nothing Then Return Value.ToString()
            End If
        Catch ex As Exception
            '忽略
        End Try
        Return DefaultValue
    End Function

    ''' <summary>
    ''' 调用约定的无参方法（方法不存在时静默跳过）。
    ''' </summary>
    Private Sub InvokeMethod(Instance As Object, Name As String)
        Dim Method As MethodInfo = Instance.GetType().GetMethod(Name)
        If Method IsNot Nothing Then Method.Invoke(Instance, Nothing)
    End Sub

End Module
