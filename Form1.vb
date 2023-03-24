Imports Newtonsoft.Json.Linq
Imports System.ComponentModel
Imports System.IO
Imports System.Net
Imports System.Net.Mail
Imports System.Text.RegularExpressions
Imports Tulpep.NotificationWindow

Public Class Form1

    'New class based on webclient that allows timeout to be changed
    Public Class WebDownload

        Inherits WebClient

        Public Property Timeout As Integer

        Public Sub New()
            Me.New(60000)
        End Sub

        Public Sub New(ByVal timeout As Integer)
            Me.Timeout = timeout
        End Sub

        Protected Overrides Function GetWebRequest(ByVal address As Uri) As WebRequest
            Dim request = MyBase.GetWebRequest(address)

            If request IsNot Nothing Then
                request.Timeout = Me.Timeout
            End If

            Return request
        End Function

    End Class

    'Declare global variables
    Dim parsejson As JObject 'JSON Object to hold API data
    Dim json As String
    Dim MaxBlockHeight As Integer
    Dim CurrentAgentVersion As String
    Dim LastAPIUpdate As Date
    Dim ForceCloseFlag As Boolean
    Dim StatusColour As String
    Dim UpToDateColour As String
    Dim CurrentColour As String
    Dim LogFileName As String
    Dim MapCacheFileName As String
    Dim JSONFileName As String
    Dim IPLocations(,) As String
    Dim CacheCounter As Integer = 0
    Dim MapRefreshCounter As Integer = 0
    Dim FileWriteThread As Threading.Thread
    Dim FormLoadComplete As Boolean = False
    Dim PortArray(,) As Integer
    Dim imageMarker As Image

    ReadOnly All As String = "- All -"

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        Try
            'Create an instance of the splashscreen
            Dim SplashScreen As Splash = New Splash

            'Show the splashscreen
            SplashScreen.Show()

            'Initialise logging
            Setup_Logging()

            'Load persistent notification settings required for logging
            chkApplicationNotification.Checked = My.Settings.ApplicationNotification
            chkWindowsNotification.Checked = My.Settings.WindowsNotification
            chkAllowLogging.Checked = My.Settings.AllowLogging
            chkAllowEmailNotification.Checked = My.Settings.AllowEmailNotification
            comAppNotifLvl.Text = My.Settings.ApplicationNotificationLevel
            comWinNotifLvl.Text = My.Settings.WindowsNotificationLevel
            comLogLvl.Text = My.Settings.LoggingLevel
            comEmailNotifLvl.Text = My.Settings.EmailNotificationLevel

            Notification_Display("Information", "Application Load has started")

            'Load remaining persistent settings
            CurrentAgentVersion = My.Settings.CurrentAgentVersion
            comStatistics.Text = My.Settings.Statistics
            lblGreenToYellow.Text = My.Settings.GreenToYellow
            lblYellowToRed.Text = My.Settings.YellowToRed
            txtIPAddress.Text = My.Settings.IPAddress
            txtPort.Text = My.Settings.NodePort
            chkHideTrayIcon.Checked = My.Settings.HideTrayIcon
            chkMinimiseToTray.Checked = My.Settings.MinimiseToTray
            chkMinimiseOnClose.Checked = My.Settings.MinimiseOnClose
            comStartup.Text = My.Settings.StartupTab
            txtSMTPHost.Text = My.Settings.SMTPHost
            txtSMTPPort.Text = My.Settings.SMTPPort
            txtSMTPUsername.Text = My.Settings.SMTPUsername
            txtSMTPPassword.Text = My.Settings.SMTPPassword
            chkUseSSL.Checked = My.Settings.UseSSL
            txtEmailAddress.Text = My.Settings.EmailAddress
            chkStartMinimised.Checked = My.Settings.StartMinimised
            chkStartWithWindows.Checked = My.Settings.StartWithWindows
            chkDesktopShortcut.Checked = My.Settings.DesktopShortcut
            chkShowTooltips.Checked = My.Settings.ShowTooltips
            chkHighlightCurrentNode.Checked = My.Settings.HighlightNode

            'check if help file present and then update visibility of inline help section on Help tab
            If File.Exists(Application.StartupPath() + "/LiteNodes.chm") Then
                gbxInlineHelp.Visible = True
            Else
                gbxInlineHelp.Visible = False
            End If

            'Update sliders to persistent values
            trkGreenToYellow.Value = lblGreenToYellow.Text
            trkYellowToRed.Value = lblYellowToRed.Text

            'Display application version information and prompt user if a new version is available
            LiteNodes_Version()
            If btnUpdateNow.Enabled = True Then
                MessageBox.Show("A new version of LiteNodes is available. Please click on the 'Update Now' button in settings to update", "LiteNodes - Update Available", MessageBoxButtons.OK, MessageBoxIcon.Information)
            End If

            'Configure map control on node map tab
            Configure_Map_Control()

            'Get map tile cache size
            Check_Map_Cache_size()

            'Set up map location cache
            Set_Up_Map_Cache()

            'Initialise JSON Persistence
            Set_Up_JSON_Persistence()

            'Load IP Locations into array
            Read_IP_Locations()

            'Read last json string into global variable
            Read_JSON_String()

            'Get current agent version (Not Blockchair API dependent)
            Get_Current_Agent_Version()

            'Load data from web URL into the JSON Object and populate application fields
            Load_JSON()

            'Configure shortcuts
            Configure_Desktop_Shortcut()
            Configure_Startup_Shortcut()

            'Centre application window on the screen
            Me.CenterToScreen()

            'Check if set to start minimised
            If chkStartMinimised.Checked = True Then
                WindowState = FormWindowState.Minimized
            Else
                WindowState = FormWindowState.Normal
            End If

            'Set visibility of tray icon
            Notify_Icon_Display()

            'set up timer
            timReloadData.Interval = 5000
            timReloadData.Enabled = True

            'Initial Check (Will be repeated at each timer tick)
            Check_For_API_Update()

            'Enable or disable the Force Close Button in settings
            Set_Force_Close_Button_Visibility()

            'Set Force close flag to false
            ForceCloseFlag = False

            'Update enabled controls in settings based on checkbox values
            Disable_Logging_Settings()
            Disable_Application_Notification_Settings()
            Disable_Windows_Notification_Settings()
            Disable_Email_Notification_Settings()

            'Open startup tab
            Open_Startup_Tab()

            'Start cache update process
            timUpdateCache.Enabled = True

            'Set up tooltips
            Configure_Tooltips()

            'Flag to indicate if form has finished loading.
            FormLoadComplete = True

            'Close the splashscreen
            SplashScreen.Close()

            Notification_Display("Information", "Application load completed successfully")

        Catch ex As Exception

            Notification_Display("Error", "There was an error in the application load", ex)

        End Try

    End Sub

    Private Sub Load_JSON()

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

        Try
            Notification_Display("Information", "The latest API download from blockchair has started")
            json = New WebDownload(2000).DownloadString("https://api.blockchair.com/litecoin/nodes")
            Notification_Display("Information", "The latest API download from blockchair has completed successfully")
        Catch ex As Exception
            If json <> "" Then
                Notification_Display("Error", "Blockchair API is unreachable. Please check network connection", ex)
                'Carry on with rest of subroutine using the last value of json stored in the global variable
            Else
                'No global variable, so put out message and close application
                MessageBox.Show("Internet connection required for application. Please try again later. Application will close", "LiteNodes - Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
                End
            End If
        End Try

        Try
            'Create JSON Object from string
            parsejson = JObject.Parse(json)

            'Save Last Updated date
            LastAPIUpdate = Convert.ToDateTime(Get_Token("context.cache.since"))

            'Update the last updated date in status bar in local time
            sslLastUpdate.Text = "Last Updated: " + LastAPIUpdate.ToLocalTime.ToString

            'Create the array for use in port statistics and port filter dropdown
            Create_Port_Array()

            'Store maximum block height
            Get_Maximum_Block_Height()

            'Populate total node count on summary tab
            lblTotalNodesValue.Text = Get_Token("data.count")

            'populate controls on node status tab
            Populate_NodeStatusTab()

            'populate Statistics
            Display_Statistics()

            'Load country dropdown list
            Load_Country_Dropdown()

            'Load height dropdown list
            Load_Height_Dropdown()

            'Load version dropdown list
            Load_Version_Dropdown()

            'Load network dropdown list
            Load_Network_Dropdown()

            'Load port dropdown list
            Load_Port_Dropdown()

            'Reset Filters as new data may not contain selections
            comCountry.Text = All
            comHeight.Text = All
            ComVersion.Text = All
            comNetwork.Text = All
            comPort.Text = All

            'populate Node List
            Load_Nodes_Datagrid()

            Notification_Display("Information", "The JSON Load has completed successfully")

        Catch ex As Exception

            Notification_Display("Error", "There was an error in the JSON load", ex)

        End Try

    End Sub

    Private Sub Populate_NodeStatusTab()

        Dim NodeList As String
        Dim ParseNode As JObject
        Dim Status As Boolean
        Dim Lag As Integer
        Dim Protocol As String

        Try
            Protocol = Validate_IPAddress(txtIPAddress.Text)

            'Check if both IP Address and Port are supplied with valid contents
            If txtIPAddress.Text = "" Or txtPort.Text = "" Or Protocol = "Invalid" Or Validate_Port(txtPort.Text) = False Then

                'Clear Details
                lblVersionValue.Text = ""
                lblCountryValue.Text = ""
                lblHeightValue.Text = ""
                lblProtocolValue.Text = ""

                'Clear Status indicators
                pbxStatus.Image = My.Resources.Grey
                StatusColour = "Grey"
                pbxUpToDate.Image = My.Resources.Grey
                UpToDateColour = "Grey"
                pbxCurrent.Image = My.Resources.Grey
                CurrentColour = "Grey"

                Notification_Display("Warning", "IP Address or Port are invalid")

            Else

                Notification_Display("Information", "IP Address and Port are valid")

                'IP Address valid, so call routine to get additional IP Address information
                Populate_IP_Address_Details()

                'Return the data about an individual Node
                NodeList = parsejson.SelectToken("data.nodes").ToString
                ParseNode = JObject.Parse(NodeList)

                'Update Details
                If ParseNode.ContainsKey(txtIPAddress.Text + ":" + txtPort.Text) Then

                    'Save Status for later
                    Status = True

                    'Populate Details
                    lblVersionValue.Text = Get_Token("data.nodes.['" + txtIPAddress.Text + ":" + txtPort.Text + "'].version")
                    lblCountryValue.Text = Get_Token("data.nodes.['" + txtIPAddress.Text + ":" + txtPort.Text + "'].country")
                    lblHeightValue.Text = Get_Token("data.nodes.['" + txtIPAddress.Text + ":" + txtPort.Text + "'].height")
                    lblProtocolValue.Text = Protocol
                Else

                    'Save Status for later
                    Status = False

                    'Clear Details
                    lblVersionValue.Text = ""
                    lblCountryValue.Text = ""
                    lblHeightValue.Text = ""
                    lblProtocolValue.Text = ""
                End If

                'Update Status Indicator
                If Status = True Then
                    pbxStatus.Image = My.Resources.Green
                    StatusColour = "Green"
                    Notification_Display("Information", "Node is online")
                Else
                    pbxStatus.Image = My.Resources.Red
                    StatusColour = "Red"
                    Notification_Display("Warning", "Node is offline")
                End If

                'Update Up-To-Date Indicator
                If Status = True Then
                    Lag = MaxBlockHeight - lblHeightValue.Text
                    If Lag < 0 Then
                        pbxUpToDate.Image = My.Resources.Grey
                        UpToDateColour = "Grey"
                        Notification_Display("Warning", "Node block height is invalid")
                    ElseIf Lag >= 0 And Lag < lblGreenToYellow.Text Then
                        pbxUpToDate.Image = My.Resources.Green
                        UpToDateColour = "Green"
                        Notification_Display("Information", "Node block height is up to date")
                    ElseIf Lag >= lblGreenToYellow.Text And Lag < lblYellowToRed.Text Then
                        pbxUpToDate.Image = My.Resources.Yellow
                        UpToDateColour = "Yellow"
                        Notification_Display("Warning", "Node block height is slightly behind")
                    Else
                        pbxUpToDate.Image = My.Resources.Red
                        UpToDateColour = "Red"
                        Notification_Display("Warning", "Node block height is significantly behind")
                    End If
                Else
                    pbxUpToDate.Image = My.Resources.Grey
                    UpToDateColour = "Grey"
                End If

                'Update Current Indicator
                If Status = True Then
                    Dim AgentVersion As String = lblVersionValue.Text
                    If AgentVersion.Length < 14 Then
                        pbxCurrent.Image = My.Resources.Grey
                        CurrentColour = "Grey"
                        Notification_Display("Warning", "Agent version not recognised")
                    ElseIf AgentVersion.Remove(14) <> "/LitecoinCore:" Then
                        pbxCurrent.Image = My.Resources.Grey
                        CurrentColour = "Grey"
                        Notification_Display("Warning", "Agent version not recognised")
                    ElseIf AgentVersion.Length < CurrentAgentVersion.Length Then
                        pbxCurrent.Image = My.Resources.Red
                        CurrentColour = "Red"
                        Notification_Display("Warning", "Agent version is out of date")
                    ElseIf AgentVersion.Remove(CurrentAgentVersion.Length) <> CurrentAgentVersion Then
                        pbxCurrent.Image = My.Resources.Red
                        CurrentColour = "Red"
                        Notification_Display("Warning", "Agent version is out of date")
                    Else
                        pbxCurrent.Image = My.Resources.Green
                        CurrentColour = "Green"
                        Notification_Display("Information", "Agent version is up to date")
                    End If
                Else
                    pbxCurrent.Image = My.Resources.Grey
                    CurrentColour = "Grey"
                End If

            End If

            're-populate the node map if current node is displayed on map
            If chkHighlightCurrentNode.Checked = True Then
                Populate_Node_Map()
            End If

            'Set the tray icon appearance dependent on status
            Set_Tray_Icon_Appearance()

            Notification_Display("Information", "Population of the node status tab has completed successfully")

        Catch ex As Exception

            Notification_Display("Error", "There was an error populating the node status tab", ex)

        End Try

    End Sub

    Private Sub sslError_Click(sender As Object, e As EventArgs) Handles sslError.Click

        'clear error message in the status bar
        Clear_Application_Notification()

    End Sub

    Private Sub Notification_Display(Severity As String, Message As String, Optional ex As Exception = Nothing)

        Try
            'display notification in the appropriate places
            If chkApplicationNotification.Checked = True Then
                If comAppNotifLvl.Text = "Warning and Error" And Severity <> "Information" Then Display_Application_Notification(Severity, Message)
                If comAppNotifLvl.Text = "Error Only" And Severity = "Error" Then Display_Application_Notification(Severity, Message)
            End If
            If chkWindowsNotification.Checked = True Then
                If comWinNotifLvl.Text = "Warning and Error" And Severity <> "Information" Then Display_Windows_Notification(Severity, Message)
                If comWinNotifLvl.Text = "Error Only" And Severity = "Error" Then Display_Windows_Notification(Severity, Message)
            End If
            If chkAllowLogging.Checked = True Then
                If comLogLvl.Text = "Everything" Then Log_Notification(Severity, Message)
                If comLogLvl.Text = "Warning and Error" And Severity <> "Information" Then Log_Notification(Severity, Message)
                If comLogLvl.Text = "Error Only" And Severity = "Error" Then Log_Notification(Severity, Message)
                If comLogLvl.Text = "Debug" And Severity = "Error" And Not ex Is Nothing Then Log_Notification(Severity, Message, ex)
            End If
            If chkAllowEmailNotification.Checked = True Then
                If comEmailNotifLvl.Text = "Warning and Error" And Severity <> "Information" Then Send_Email_Notification(Severity, Message)
                If comEmailNotifLvl.Text = "Error Only" And Severity = "Error" Then Send_Email_Notification(Severity, Message)
            End If

        Catch
            sslError.Text = "There has been a critical error in the notification display flow"
            sslError.BackColor = Color.Red
            sslError.ForeColor = Color.White
        End Try

    End Sub

    Private Sub Display_Application_Notification(Severity As String, Message As String)

        Try
            'display notification in the status bar
            sslError.Text = Message
            Select Case Severity
                Case "Warning"
                    sslError.BackColor = Color.Yellow
                    sslError.ForeColor = Color.Black
                Case "Error"
                    sslError.BackColor = Color.Red
                    sslError.ForeColor = Color.White
            End Select

            'Set Timer to 8 seconds to clear notification
            timClearError.Interval = 8000

            'Reset timer in case its already running
            timClearError.Enabled = False
            timClearError.Enabled = True

        Catch
            sslError.Text = "There has been a critical error in the application notification display"
            sslError.BackColor = Color.Red
            sslError.ForeColor = Color.White
        End Try

    End Sub

    Private Sub Clear_Application_Notification()

        'Clear notification in the status bar
        sslError.Text = ""
        sslError.BackColor = Control.DefaultBackColor

    End Sub

    Private Function Get_Token(Token As String) As String

        Dim Value As String

        Try
            'Get a single token from the JSON object
            Value = parsejson.SelectToken(Token).ToString

            Notification_Display("Information", "The value of " + Value + " was returned successfully for token " + Token)
        Catch ex As Exception
            Notification_Display("Error", "There was a problem retrieving a value for token " + Token, ex)
            Value = ""
        End Try

        Return Value

    End Function

    Private Sub txtIPAddress_TextChanged(sender As Object, e As EventArgs) Handles txtIPAddress.TextChanged

        'Only refresh if user making change
        If TabControl1.SelectedTab Is tabNodestatus Then
            'Reset timer. Node status tab will be refreshed when the timer elapsed
            timTextbox.Enabled = False
            timTextbox.Enabled = True
        End If

    End Sub

    Private Sub txtPort_TextChanged(sender As Object, e As EventArgs) Handles txtPort.TextChanged

        'Only refresh if user making change
        If TabControl1.SelectedTab Is tabNodestatus Then
            'Reset timer. Node status tab will be refreshed when the timer elapsed
            timTextbox.Enabled = False
            timTextbox.Enabled = True
        End If

    End Sub

    Private Function Validate_IPAddress(Input As String) As String

        Dim Result As String

        Try
            'Check if supplied string is a valid IP Address
            If Regex.IsMatch(Input, "\b(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\b") Then
                Result = "IPv4"
            ElseIf Regex.IsMatch(Input, "(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9]))") Then
                Result = "IPv6"
            Else
                Result = "Invalid"
            End If

            Notification_Display("Information", "The IP Address " + Input + " was found to be " + Result)

        Catch ex As Exception
            Notification_Display("Error", "There was an error in validating the IP Address " + Input, ex)
            Result = ""
        End Try

        Return Result

    End Function

    Private Function Validate_Port(Input As String) As Boolean

        Dim Valid As Boolean

        Try
            'Check if supplied string is a valid Port
            Valid = Regex.IsMatch(Input, "^([1-9][0-9]{0,3}|[1-5][0-9]{4}|6[0-4][0-9]{3}|65[0-4][0-9]{2}|655[0-2][0-9]|6553[0-5])$")

            Notification_Display("Information", "The port " + Input + " was found to be " + Valid.ToString)

        Catch ex As Exception
            Notification_Display("Error", "There was an error in validating the port " + Input, ex)
            Valid = Nothing
        End Try

        Return Valid

    End Function

    Private Sub Timer1_Tick(sender As Object, e As EventArgs) Handles timReloadData.Tick

        Check_For_API_Update()

    End Sub

    Private Sub Load_Country_Datagrid()

        'Local Variables
        Dim Country As String()
        Dim Count As Integer = 0
        Dim MaxResult As Integer

        Try
            Dim parsecountry As JObject = parsejson.SelectToken("data.countries")
            Dim countries As List(Of JToken) = parsecountry.Children().ToList()

            'Set appropriate column header
            grdStatistics.Columns(0).HeaderText = "Country"

            'Populate data to rows and columns
            For Each JToken In countries
                Country = JToken.ToString.Split(New Char() {":"c})
                Country(0) = Country(0).Trim("""")
                grdStatistics.Rows.Add()
                grdStatistics.Rows(Count).Cells(0).Value = Country(0)
                grdStatistics.Rows(Count).Cells(1).Value = Country(1)
                If Count = 0 Then MaxResult = Country(1)

                'Display Status Bar
                grdStatistics.Rows(Count).Cells(2).Value = BarChartText(MaxResult, Country(1))

                Count += 1
            Next

            'Display Row Count
            lblRowCount.Text = Count.ToString
            lblRows.Text = "Countries"

            Notification_Display("Information", "The country datagrid was loaded successfully with " + Count.ToString + " rows")

        Catch ex As Exception
            Notification_Display("Error", "There was an error in loading the country datagrid", ex)
        End Try

    End Sub

    Private Sub Load_Height_Datagrid()

        'Local Variables
        Dim Height As String()
        Dim Count As Integer = 0
        Dim MaxResult As Integer

        Try
            Dim parseheight As JObject = parsejson.SelectToken("data.heights")
            Dim heights As List(Of JToken) = parseheight.Children().ToList()

            'Set appropriate column header
            grdStatistics.Columns(0).HeaderText = "Height"

            'Populate data to rows and columns
            For Each JToken In heights
                Height = JToken.ToString.Split(New Char() {":"c})
                Height(0) = Height(0).Trim("""")
                grdStatistics.Rows.Add()
                grdStatistics.Rows(Count).Cells(0).Value = Height(0)
                grdStatistics.Rows(Count).Cells(1).Value = Height(1)
                If Count = 0 Then MaxResult = Height(1)

                'Display Status Bar
                grdStatistics.Rows(Count).Cells(2).Value = BarChartText(MaxResult, Height(1))

                Count += 1
            Next

            'Display Row Count
            lblRowCount.Text = Count.ToString
            lblRows.Text = "Heights"

            Notification_Display("Information", "The height datagrid was loaded successfully with " + Count.ToString + " rows")

        Catch ex As Exception
            Notification_Display("Error", "There was an error in loading the height datagrid", ex)
        End Try

    End Sub

    Private Sub Load_Version_Datagrid()

        'Local Variables
        Dim VersionString As String
        Dim Version As String()
        Dim Count As Integer = 0
        Dim MaxResult As Integer

        Try
            Dim parseversion As JObject = parsejson.SelectToken("data.versions")
            Dim versions As List(Of JToken) = parseversion.Children().ToList()

            grdStatistics.Columns(0).HeaderText = "Version"

            'Populate data to rows and columns
            For Each JToken In versions
                VersionString = JToken.ToString.Replace(": ", ":" + ChrW(&H2588))
                Version = VersionString.Split(New Char() {ChrW(&H2588)})
                Version(0) = Version(0).Trim(":")
                Version(0) = Version(0).Trim("""")
                grdStatistics.Rows.Add()
                grdStatistics.Rows(Count).Cells(0).Value = Version(0)
                grdStatistics.Rows(Count).Cells(1).Value = Version(1)
                If Count = 0 Then MaxResult = Version(1)

                'Display Status Bar
                grdStatistics.Rows(Count).Cells(2).Value = BarChartText(MaxResult, Version(1))

                Count += 1
            Next

            'Display Row Count
            lblRowCount.Text = Count.ToString
            lblRows.Text = "Versions"

            Notification_Display("Information", "The version datagrid was loaded successfully with " + Count.ToString + " rows")

        Catch ex As Exception
            Notification_Display("Error", "There was an error in loading the version datagrid", ex)
        End Try

    End Sub
    Private Sub Load_Protocol_Datagrid()

        'Local Variables
        Dim Token As String()
        Dim IPv6 As Integer = 0
        Dim IPv4 As Integer = 0
        Dim MaxResult As Integer

        Try
            Dim parsenodes As JObject = parsejson.SelectToken("data.nodes")
            Dim nodes As List(Of JToken) = parsenodes.Children().ToList()

            grdStatistics.Columns(0).HeaderText = "Protocol"

            'Calculate counts of protocols
            For Each JToken In nodes
                Token = JToken.ToString.Split(New Char() {""""c})
                If Token(1).Contains(".") Then
                    IPv4 += 1
                Else
                    IPv6 += 1
                End If
            Next

            grdStatistics.Rows.Add(2)

            If IPv4 > IPv6 Then
                MaxResult = IPv4
                grdStatistics.Rows(0).Cells(0).Value = "IPv4"
                grdStatistics.Rows(0).Cells(1).Value = IPv4
                grdStatistics.Rows(0).Cells(2).Value = BarChartText(MaxResult, IPv4)
                grdStatistics.Rows(1).Cells(0).Value = "IPv6"
                grdStatistics.Rows(1).Cells(1).Value = IPv6
                grdStatistics.Rows(1).Cells(2).Value = BarChartText(MaxResult, IPv6)
            Else
                MaxResult = IPv6
                grdStatistics.Rows(0).Cells(0).Value = "IPv6"
                grdStatistics.Rows(0).Cells(1).Value = IPv6
                grdStatistics.Rows(0).Cells(2).Value = BarChartText(MaxResult, IPv6)
                grdStatistics.Rows(1).Cells(0).Value = "IPv4"
                grdStatistics.Rows(1).Cells(1).Value = IPv4
                grdStatistics.Rows(1).Cells(2).Value = BarChartText(MaxResult, IPv4)
            End If

            'Display Row Count
            lblRowCount.Text = 2
            lblRows.Text = "Protocols"

            Notification_Display("Information", "The protocol datagrid was loaded successfully with 2 rows")

        Catch ex As Exception
            Notification_Display("Error", "There was an error in loading the protocol datagrid", ex)
        End Try

    End Sub

    Private Sub Load_Port_Datagrid()

        'Local Variables
        Dim Count As Integer
        Dim MaxResult As Integer = 0

        Try
            'Set appropriate column header
            grdStatistics.Columns(0).HeaderText = "Port"

            'Populate data to rows and columns and get the highest count of ports
            For Count = 0 To PortArray.GetLength(1) - 2 'Last row in array is blank
                grdStatistics.Rows.Add()
                grdStatistics.Rows(Count).Cells(0).Value = PortArray(0, Count)
                grdStatistics.Rows(Count).Cells(1).Value = PortArray(1, Count)
                If PortArray(1, Count) > MaxResult Then MaxResult = PortArray(1, Count)
            Next

            'Sort datagrid on count descending
            grdStatistics.Sort(grdStatistics.Columns(1), ListSortDirection.Descending)

            'Display Status Bars
            For i As Integer = 0 To Count - 1
                grdStatistics.Rows(i).Cells(2).Value = BarChartText(MaxResult, grdStatistics.Rows(i).Cells(1).Value)
            Next

            'Display Row Count
            lblRowCount.Text = Count.ToString
            lblRows.Text = "Ports"

            Notification_Display("Information", "The port datagrid was loaded successfully with " + Count.ToString + " rows")

        Catch ex As Exception
            Notification_Display("Error", "There was an error in loading the port datagrid", ex)
        End Try

    End Sub

    Private Sub Display_Statistics()

        Try
            'Clear Datagrid
            grdStatistics.Rows.Clear()

            'Choose correct method to populate datagrid
            If comStatistics.Text = "Country" Then
                Load_Country_Datagrid()
            ElseIf comStatistics.Text = "Height" Then
                Load_Height_Datagrid()
            ElseIf comStatistics.Text = "Version" Then
                Load_Version_Datagrid()
            ElseIf comStatistics.Text = "Protocol" Then
                Load_Protocol_Datagrid()
            ElseIf comStatistics.Text = "Port" Then
                Load_Port_Datagrid()
            Else
                'If selection not recognised then default to Country
                comStatistics.Text = "Country"
                Load_Country_Datagrid()
            End If

            Notification_Display("Information", "The statistics datagrid has been successfully selected as " + comStatistics.Text)
        Catch ex As Exception
            Notification_Display("Error", "There was an error in selecting the statistics datagrid", ex)
        End Try

    End Sub

    Private Sub comStatistics_SelectionChangeCommitted(sender As Object, e As EventArgs) Handles comStatistics.SelectionChangeCommitted

        Display_Statistics()

        'Remove Focus from dropdown by setting focus to label (Any non editable control would do)
        lblStatisticsSelect.Focus()

    End Sub

    Private Function BarChartText(Maximum As Integer, Value As Integer) As String

        'Return a string of blocks whose length relates to the percentage of maximum value
        Dim BarChart As String = ""

        Try
            Dim Count As Decimal = 27 * Value / Maximum
            Dim CountInt As Integer = Math.Truncate(Count)

            Count -= CountInt

            While CountInt > 0
                BarChart += ChrW(&H2589)
                CountInt -= 1
            End While

            If CountInt = 0 And Count < 0.25 Then BarChart += ChrW(&H258F)
            If Count > 0.25 And Count <= 0.5 Then BarChart += ChrW(&H258F)
            If Count > 0.5 And Count <= 0.75 Then BarChart += ChrW(&H258E)
            If Count > 0.75 And Count < 1 Then BarChart += ChrW(&H258D)

        Catch ex As Exception
            Notification_Display("Error", "There was an error generating the barchart text", ex)
        End Try

        Return BarChart

    End Function

    Private Sub Load_Nodes_Datagrid()

        'Local Variables
        Dim Token As String()
        Dim Node As String()
        Dim Height As String = ""
        Dim Count As Integer = 0
        Dim Include As Boolean
        Dim IPAddress As String
        Dim Port As String
        Dim n As Integer

        Try
            Dim parsenodes As JObject = parsejson.SelectToken("data.nodes")
            Dim nodes As List(Of JToken) = parsenodes.Children().ToList()

            'Clear Node List
            grdNodeList.Rows.Clear()

            'Populate data to rows and columns
            For Each JToken In nodes

                Token = JToken.ToString.Split(New Char() {""""c})
                Include = True

                'Filter By country, height and version
                If comCountry.Text <> All And Token(9) <> comCountry.Text Then Include = False
                If comHeight.Text <> All Then Height = Regex.Replace(Token(12), "[^\d]", "")
                If comHeight.Text <> All And Height <> comHeight.Text Then Include = False
                If ComVersion.Text <> All And Token(5) <> ComVersion.Text Then Include = False

                'Continue if meets country, height and version filters
                If Include = True Then
                    Node = Token(1).Split(New Char() {":"c})
                    IPAddress = Node(0)
                    For n = 1 To Node.Length - 2
                        IPAddress += ":" + Node(n)
                    Next
                    Port = Node(Node.Length - 1)
                    IPAddress = IPAddress.Trim("""")
                    Port = Port.Trim("""")

                    'Filter by network protocol and port
                    If comNetwork.Text = All Or (comNetwork.Text = "IPv4" And IPAddress.Contains(".")) Or (comNetwork.Text = "IPv6" And IPAddress.Contains(":")) Then
                        If comPort.Text = All Or comPort.Text = Port Then
                            grdNodeList.Rows.Add()
                            grdNodeList.Rows(Count).Cells(0).Value = IPAddress
                            grdNodeList.Rows(Count).Cells(1).Value = Port
                            Count += 1
                        End If
                    End If
                End If
            Next

            'Display number of nodes found
            lblNodeRowsCount.Text = Count.ToString

            'Redraw the nodes map
            Populate_Node_Map()

            Notification_Display("Information", "The nodes datagrid was loaded successfully with " + Count.ToString + " rows")

        Catch ex As Exception
            Notification_Display("Error", "There was an error in loading the nodes datagrid", ex)
        End Try

    End Sub

    Private Sub Load_Country_Dropdown()

        'Local Variables
        Dim Country As String()

        Try
            Dim parsecountry As JObject = parsejson.SelectToken("data.countries")
            Dim countries As List(Of JToken) = parsecountry.Children().ToList()

            'Clear existing list
            comCountry.Items.Clear()

            'Add an All option at start of list
            comCountry.Items.Add(All)

            'Populate data to dropdown list
            For Each JToken In countries
                Country = JToken.ToString.Split(New Char() {":"c})
                Country(0) = Country(0).Trim("""")
                comCountry.Items.Add(Country(0))
            Next

            Notification_Display("Information", "The country dropdown has been populated successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error populating the country dropdown", ex)
        End Try

    End Sub

    Private Sub Load_Height_Dropdown()

        'Local Variables
        Dim Height As String()

        Try
            Dim parseheight As JObject = parsejson.SelectToken("data.heights")
            Dim heights As List(Of JToken) = parseheight.Children().ToList()

            'Clear existing list
            comHeight.Items.Clear()

            'Add an All option at start of list
            comHeight.Items.Add(All)

            'Populate data to dropdown list
            For Each JToken In heights
                Height = JToken.ToString.Split(New Char() {":"c})
                Height(0) = Height(0).Trim("""")
                comHeight.Items.Add(Height(0))
            Next

            Notification_Display("Information", "The height dropdown has been populated successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error populating the height dropdown", ex)
        End Try

    End Sub

    Private Sub Load_Version_Dropdown()

        'Local Variables
        Dim VersionString As String
        Dim Version As String()

        Try
            Dim parseversion As JObject = parsejson.SelectToken("data.versions")
            Dim versions As List(Of JToken) = parseversion.Children().ToList()

            'Clear existing list
            ComVersion.Items.Clear()

            'Add an All option at start of list
            ComVersion.Items.Add(All)

            'Populate data to dropdown list
            For Each JToken In versions
                VersionString = JToken.ToString.Replace(": ", ":" + ChrW(&H2588))
                Version = VersionString.Split(New Char() {ChrW(&H2588)})
                Version(0) = Version(0).Trim(":")
                Version(0) = Version(0).Trim("""")
                ComVersion.Items.Add(Version(0))
            Next

            Notification_Display("Information", "The version dropdown has been populated successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error populating the version dropdown", ex)
        End Try

    End Sub

    Private Sub Load_Network_Dropdown()

        Try
            'Clear existing list
            comNetwork.Items.Clear()

            'Add an All option at start of list
            comNetwork.Items.Add(All)

            'Populate remaining values
            comNetwork.Items.Add("IPv4")
            comNetwork.Items.Add("IPv6")

            Notification_Display("Information", "The network dropdown has been populated successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error populating the network dropdown", ex)
        End Try

    End Sub

    Private Sub Load_Port_Dropdown()

        Try
            'Clear existing list
            comPort.Items.Clear()

            'Add an All option at start of list
            comPort.Items.Add(All)

            'Populate port values to single dimension array to allow sorting
            Dim Length As Integer = PortArray.GetLength(1) - 1
            Dim PortList(Length) As Integer
            For i As Integer = 0 To Length
                PortList(i) = PortArray(0, i)
            Next

            'Sort list descending
            Array.Sort(PortList)
            Array.Reverse(PortList)

            'Add sorted items to dropdown
            For i As Integer = 0 To Length
                comPort.Items.Add(PortList(i))
            Next

            Notification_Display("Information", "The port dropdown has been populated successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error populating the port dropdown", ex)
        End Try

    End Sub

    Private Sub comCountry_SelectionChangeCommitted(sender As Object, e As EventArgs) Handles comCountry.SelectionChangeCommitted

        Load_Nodes_Datagrid()

    End Sub

    Private Sub comHeight_SelectionChangeCommitted(sender As Object, e As EventArgs) Handles comHeight.SelectionChangeCommitted

        Load_Nodes_Datagrid()

    End Sub

    Private Sub ComVersion_SelectionChangeCommitted(sender As Object, e As EventArgs) Handles ComVersion.SelectionChangeCommitted

        Load_Nodes_Datagrid()

    End Sub

    Private Sub comNetwork_SelectionChangeCommitted(sender As Object, e As EventArgs) Handles comNetwork.SelectionChangeCommitted

        Load_Nodes_Datagrid()

    End Sub

    Private Sub comPort_SelectionChangeCommitted(sender As Object, e As EventArgs) Handles comPort.SelectionChangeCommitted

        Load_Nodes_Datagrid()

    End Sub

    Private Sub grdNodeList_CellContentClick(sender As Object, e As DataGridViewCellEventArgs) Handles grdNodeList.CellContentClick

        Try
            'Set IP Address and Port Values in Node Details tab
            txtIPAddress.Text = grdNodeList.Rows(e.RowIndex).Cells(0).Value
            txtPort.Text = grdNodeList.Rows(e.RowIndex).Cells(1).Value

            'Refresh the node details
            Populate_NodeStatusTab()

            'Switch to Node Status tab
            TabControl1.SelectedTab = tabNodestatus

            'Move focus away from IPAddress textbox to make tab look nicer (its irrelevant where focus is set to)
            gbxStatus.Focus()

            Notification_Display("Information", "The node " + txtIPAddress.Text + ":" + txtPort.Text + " was successfully selected from the node list")
        Catch ex As Exception
            Notification_Display("Error", "There was an error selecting the node from the node list", ex)
        End Try

    End Sub

    Private Sub lblTotalNodesValue_Click(sender As Object, e As EventArgs) Handles lblTotalNodesValue.Click

        Try
            'Reset all filters to show all nodes
            comCountry.Text = All
            comHeight.Text = All
            ComVersion.Text = All
            comNetwork.Text = All
            comPort.Text = All

            'Refresh node list
            Load_Nodes_Datagrid()

            'Switch to Node List tab
            TabControl1.SelectedTab = tabNodeList

            Notification_Display("Information", "The node list for all nodes has been displayed successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the node list for all nodes", ex)
        End Try

    End Sub

    Private Sub btnClearFilters_Click(sender As Object, e As EventArgs) Handles btnClearFilters.Click

        Try
            'Reset all filters to show all nodes
            comCountry.Text = All
            comHeight.Text = All
            ComVersion.Text = All
            comNetwork.Text = All
            comPort.Text = All

            Load_Nodes_Datagrid()

            Notification_Display("Information", "All the node list filters have been cleared successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error clearing all the node list filters", ex)
        End Try

    End Sub

    Private Sub grdStatistics_CellContentClick(sender As Object, e As DataGridViewCellEventArgs) Handles grdStatistics.CellContentClick

        Try
            'Set Search Parameters dependent on which Statistics are being shown
            If comStatistics.Text = "Country" Then
                comCountry.Text = grdStatistics.Rows(e.RowIndex).Cells(0).Value
                comHeight.Text = All
                ComVersion.Text = All
                comNetwork.Text = All
                comPort.Text = All
            ElseIf comStatistics.Text = "Height" Then
                comHeight.Text = grdStatistics.Rows(e.RowIndex).Cells(0).Value
                comCountry.Text = All
                ComVersion.Text = All
                comNetwork.Text = All
                comPort.Text = All
            ElseIf comStatistics.Text = "Version" Then
                ComVersion.Text = grdStatistics.Rows(e.RowIndex).Cells(0).Value
                comCountry.Text = All
                comHeight.Text = All
                comNetwork.Text = All
                comPort.Text = All
            ElseIf comStatistics.Text = "Protocol" Then
                comNetwork.Text = grdStatistics.Rows(e.RowIndex).Cells(0).Value
                comCountry.Text = All
                comHeight.Text = All
                ComVersion.Text = All
                comPort.Text = All
            ElseIf comStatistics.Text = "Port" Then
                comPort.Text = grdStatistics.Rows(e.RowIndex).Cells(0).Value
                comNetwork.Text = All
                comCountry.Text = All
                comHeight.Text = All
                ComVersion.Text = All
            Else
                comCountry.Text = All
                comHeight.Text = All
                ComVersion.Text = All
                comNetwork.Text = All
                comPort.Text = All
            End If

            'Refresh grid contents
            Load_Nodes_Datagrid()

            'Switch to Node List tab
            TabControl1.SelectedTab = tabNodeList

            Notification_Display("Information", "Node list has been made active with the " + comStatistics.Text + " filter set to " + grdStatistics.Rows(e.RowIndex).Cells(0).Value.ToString)
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the node list filtered on " + comStatistics.Text, ex)
        End Try

    End Sub

    Private Sub Check_For_API_Update()

        Try
            Dim Difference As Long
            Dim Percentage As Integer

            'Check progress towards new update
            Difference = DateDiff(DateInterval.Second, LastAPIUpdate, Date.UtcNow)

            'Reload data from API into JSON Object if more than 10 minutes since last update
            If Difference > 600 Then
                Load_JSON()
                'Get new difference
                Difference = DateDiff(DateInterval.Second, LastAPIUpdate, Date.UtcNow)
            End If

            'Display progress in progress bar
            Percentage = Difference / 6
            If Percentage < 0 Then Percentage = 0
            If Percentage > 100 Then Percentage = 100
            sslAPIProgressBar.Value = Percentage

            'Do not log a success message as there would be one message per 5 seconds in the log!!
        Catch ex As Exception
            Notification_Display("Error", "There was an error during the regular check for an API update", ex)
        End Try

    End Sub

    Private Sub Get_Current_Agent_Version()

        'Load the data from the github API into a JSON object

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

        Dim jsonRelease As String
        Dim client As New WebDownload(2000)

        Try
            client.Headers.Add("user-agent", "request")

            Try
                Notification_Display("Information", "The API download from github has started")
                jsonRelease = client.DownloadString("https://api.github.com/repos/litecoin-project/litecoin/releases/latest")
                Notification_Display("Information", "The API download from github has completed successfully")
            Catch ex As Exception
                Notification_Display("Error", "Github API is unreachable. Please check network connection", ex)
                'Exit the sub leaving the last retrieved agent version in the global variable
                Exit Sub
            End Try

            Dim parseRelease As JObject = JObject.Parse(jsonRelease)

            Dim Version As String = parseRelease.SelectToken("tag_name").ToString
            Version = Version.TrimStart("v")
            Version = "/LitecoinCore:" + Version

            CurrentAgentVersion = Version

            Notification_Display("Information", "The current agent version has been successfully identified as " + Version)
        Catch ex As Exception
            Notification_Display("Error", "There was an error identifying the current agent version. It will be left as " + CurrentAgentVersion, ex)
        End Try

    End Sub

    Private Sub Get_Maximum_Block_Height()

        Try
            'Local Variables
            Dim parseheight As JObject = parsejson.SelectToken("data.heights")
            Dim heights As List(Of JToken) = parseheight.Children().ToList()
            Dim Height As String()
            Dim LatestBlockHeight As Integer

            'Get Latest block height
            LatestBlockHeight = Get_Token("context.state")
            LatestBlockHeight += 20

            'initiate max block height to zero
            MaxBlockHeight = 0

            'Cycle through all heights to find the highest
            For Each JToken In heights
                Height = JToken.ToString.Split(New Char() {":"c})
                Height(0) = Height(0).Trim("""")
                If Height(0) > MaxBlockHeight And Height(0) <= LatestBlockHeight Then MaxBlockHeight = Height(0)
            Next

            Notification_Display("Information", "The maximum block height has been successfully identified as " + MaxBlockHeight.ToString)
        Catch ex As Exception
            Notification_Display("Error", "There was an error identifying the maximum block height", ex)
        End Try

    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing

        Try
            'Minimise instead of close
            If chkMinimiseOnClose.Checked = True And ForceCloseFlag = False And e.CloseReason = CloseReason.UserClosing Then
                WindowState = FormWindowState.Minimized
                e.Cancel = True
                Notification_Display("Information", "The application is being minimised")
            Else
                Notification_Display("Information", "The application is being closed")
            End If

            'Set Force close flag to false
            ForceCloseFlag = False

        Catch ex As Exception
            Notification_Display("Error", "There was an error closing/minimising the application", ex)
        End Try

    End Sub

    Private Sub btnRestoreDefaults_Click(sender As Object, e As EventArgs) Handles btnRestoreDefaults.Click

        Try
            'Get Confirmation
            If Request_Confirmation("This will lose all your personalised settings") = True Then
                lblGreenToYellow.Text = My.Settings.GreenToYellowDefault
                lblYellowToRed.Text = My.Settings.YellowToRedDefault
                chkHideTrayIcon.Checked = My.Settings.HideTrayIconDefault
                chkMinimiseToTray.Checked = My.Settings.MinimiseToTrayDefault
                chkMinimiseOnClose.Checked = My.Settings.MinimiseOnCloseDefault
                comStartup.Text = My.Settings.StartupTabDefault
                chkApplicationNotification.Checked = My.Settings.ApplicationNotificationDefault
                chkWindowsNotification.Checked = My.Settings.WindowsNotificationDefault
                chkAllowLogging.Checked = My.Settings.AllowLoggingDefault
                chkAllowEmailNotification.Checked = My.Settings.AllowEmailNotificationDefault
                comAppNotifLvl.Text = My.Settings.ApplicationNotificationLevelDefault
                comWinNotifLvl.Text = My.Settings.WindowsNotificationLevelDefault
                comLogLvl.Text = My.Settings.LoggingLevelDefault
                comEmailNotifLvl.Text = My.Settings.EmailNotificationLevelDefault
                chkStartMinimised.Checked = My.Settings.StartMinimisedDefault
                chkStartWithWindows.Checked = My.Settings.StartWithWindowsDefault
                chkDesktopShortcut.Checked = My.Settings.DesktopShortcutDefault
                chkShowTooltips.Checked = My.Settings.ShowTooltipsDefault
                chkHighlightCurrentNode.Checked = My.Settings.HighlightNodeDefault

                'Update sliders
                trkGreenToYellow.Value = lblGreenToYellow.Text
                trkYellowToRed.Value = lblYellowToRed.Text

                Notification_Display("Information", "The default settings have been restored successfully")
            End If


        Catch ex As Exception
            Notification_Display("Error", "There was an error restoring the default settings", ex)
        End Try

    End Sub

    Private Sub chkHideTrayIcon_CheckedChanged(sender As Object, e As EventArgs) Handles chkHideTrayIcon.CheckedChanged

        Notify_Icon_Display()

    End Sub

    Private Sub Notify_Icon_Display()

        Try
            'Set visibility of tray icon
            If chkHideTrayIcon.Checked = True Then
                NotifyIcon1.Visible = False
                Notification_Display("Information", "The tray icon has been hidden")
            Else
                NotifyIcon1.Visible = True
                Notification_Display("Information", "The tray icon has been made visible")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error changing the tray icon visibility", ex)
        End Try

    End Sub

    Private Sub Form1_Resize(sender As Object, e As EventArgs) Handles Me.Resize

        Try
            'Handle the minimize event
            If WindowState = FormWindowState.Minimized Then
                Recreate_Map_Control()
                If chkMinimiseToTray.Checked = True Then
                    NotifyIcon1.Visible = True
                    ShowInTaskbar = False
                    Notification_Display("Information", "The application has been minimised to the tray")
                Else
                    Notification_Display("Information", "The application has been minimised to the taskbar")
                End If
            End If

            'Handle the restore event to reset selected display of tray icon
            If WindowState = FormWindowState.Normal Then
                Me.Visible = False
                Notify_Icon_Display()
                ShowInTaskbar = True
                Me.Visible = True
                Notification_Display("Information", "The application window has been displayed")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error minimising or restoring the application", ex)
        End Try

    End Sub

    Private Sub Form1_Closed(sender As Object, e As EventArgs) Handles Me.Closed

        Try
            If FormLoadComplete = True Then

                'Only save all the persistent settings if the form load completed successfully

                Save_Settings()

                'Save json global variable to text file in case internet not available on restart
                Write_JSON_String()

                'Save IP location array to text file using background thread to avoid holding up form close
                FileWriteThread = New Threading.Thread(AddressOf Write_IP_Locations)
                FileWriteThread.Start()

                Notification_Display("Information", "The persistent settings have been saved successfully on form close")
            Else
                Notification_Display("Information", "The persistent settings were not saved on form close as application load had not completed")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error saving the persistent settings on form close", ex)
        End Try

    End Sub

    Private Sub btnForceClose_Click(sender As Object, e As EventArgs) Handles btnForceClose.Click

        'Close application even if minimise on close is selected
        ForceCloseFlag = True
        Me.Close()

    End Sub

    Private Sub Set_Force_Close_Button_Visibility()

        Try
            If chkMinimiseOnClose.Checked = True Then
                btnForceClose.Enabled = True
            Else
                btnForceClose.Enabled = False
            End If

            Notification_Display("Information", "The force close button visibility has been successfully set")
        Catch ex As Exception
            Notification_Display("Error", "There was an error setting the force close button visibility", ex)
        End Try

    End Sub

    Private Sub chkMinimiseOnClose_CheckedChanged(sender As Object, e As EventArgs) Handles chkMinimiseOnClose.CheckedChanged

        Set_Force_Close_Button_Visibility()

    End Sub

    Private Sub Set_Tray_Icon_Appearance()

        'Change notify icon dependent on selected node status in the application

        Try
            If StatusColour = "Grey" Then
                'Use grey icon as Node not valid
                NotifyIcon1.Icon = My.Resources.Grey1
                NotifyIcon1.Text = "LiteNodes - Node Invalid"
            ElseIf StatusColour = "Green" And UpToDateColour = "Green" And CurrentColour = "Green" Then
                'Use green icon as all node parameters are OK
                NotifyIcon1.Icon = My.Resources.Green1
                NotifyIcon1.Text = "LiteNodes - Node Healthy"
            Else
                'At least one node parameter is showing an issue
                NotifyIcon1.Icon = My.Resources.Red1
                If StatusColour = "Red" Then
                    NotifyIcon1.Text = "LiteNodes - Node Offline"
                ElseIf UpToDateColour = "Yellow" Then
                    NotifyIcon1.Text = "LiteNodes - Node Block Height Slightly Behind"
                ElseIf UpToDateColour = "Red" Then
                    NotifyIcon1.Text = "LiteNodes - Node Block Height Significantly Behind"
                Else
                    NotifyIcon1.Text = "LiteNodes - Node Agent Out of Date"
                End If
            End If

            Notification_Display("Information", "The tray icon appearance has been set to " + NotifyIcon1.Text)
        Catch ex As Exception
            Notification_Display("Error", "There was an error setting the tray icon appearance", ex)
        End Try

    End Sub

    Private Sub Open_Startup_Tab()

        Try
            Select Case comStartup.Text
                Case "Summary"
                    TabControl1.SelectedTab = tabSummary
                Case "Statistics"
                    TabControl1.SelectedTab = tabStatistics
                Case "Node List"
                    TabControl1.SelectedTab = tabNodeList
                Case "Node Map"
                    TabControl1.SelectedTab = tabNodeMap
                Case "Node Status"
                    TabControl1.SelectedTab = tabNodestatus
                    gbxStatus.Focus()
                Case "Settings"
                    TabControl1.SelectedTab = tabSettings
                Case "Help"
                    TabControl1.SelectedTab = tabHelp
                Case Else
                    TabControl1.SelectedTab = tabSummary
            End Select

            Notification_Display("Information", "The startup tab has been set to " + TabControl1.SelectedTab.ToString)
        Catch ex As Exception
            Notification_Display("Error", "There was an error opening the startup tab", ex)
        End Try

    End Sub

    Private Sub Save_Settings()

        Try
            'Save persistent settings
            My.Settings.IPAddress = txtIPAddress.Text
            My.Settings.NodePort = txtPort.Text
            My.Settings.Statistics = comStatistics.Text
            My.Settings.GreenToYellow = lblGreenToYellow.Text
            My.Settings.YellowToRed = lblYellowToRed.Text
            My.Settings.HideTrayIcon = chkHideTrayIcon.Checked
            My.Settings.MinimiseToTray = chkMinimiseToTray.Checked
            My.Settings.MinimiseOnClose = chkMinimiseOnClose.Checked
            My.Settings.StartupTab = comStartup.Text
            My.Settings.ApplicationNotification = chkApplicationNotification.Checked
            My.Settings.WindowsNotification = chkWindowsNotification.Checked
            My.Settings.AllowLogging = chkAllowLogging.Checked
            My.Settings.AllowEmailNotification = chkAllowEmailNotification.Checked
            My.Settings.ApplicationNotificationLevel = comAppNotifLvl.Text
            My.Settings.WindowsNotificationLevel = comWinNotifLvl.Text
            My.Settings.LoggingLevel = comLogLvl.Text
            My.Settings.EmailNotificationLevel = comEmailNotifLvl.Text
            My.Settings.SMTPHost = txtSMTPHost.Text
            My.Settings.SMTPPort = txtSMTPPort.Text
            My.Settings.SMTPUsername = txtSMTPUsername.Text
            My.Settings.SMTPPassword = txtSMTPPassword.Text
            My.Settings.UseSSL = chkUseSSL.Checked
            My.Settings.EmailAddress = txtEmailAddress.Text
            My.Settings.CurrentAgentVersion = CurrentAgentVersion
            My.Settings.StartMinimised = chkStartMinimised.Checked
            My.Settings.StartWithWindows = chkStartWithWindows.Checked
            My.Settings.DesktopShortcut = chkDesktopShortcut.Checked
            My.Settings.ShowTooltips = chkShowTooltips.Checked
            My.Settings.HighlightNode = chkHighlightCurrentNode.Checked

            My.Settings.Save()

            Notification_Display("Information", "The persistent application level settings have been saved successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error saving the persistent application level settings", ex)
        End Try

    End Sub

    Private Sub btnSaveSettings_Click(sender As Object, e As EventArgs) Handles btnSaveSettings.Click

        Save_Settings()

    End Sub

    Private Function Display_Windows_Notification(Severity As String, Message As String) As Boolean

        Try
            Dim Notification As New PopupNotifier()

            'Unconditional Settings
            Notification.ContentText = Message
            Notification.ImageSize = New Size(80, 80)
            Notification.TitleColor = Color.Black
            Notification.ContentColor = Color.Black
            Notification.Delay = 8000
            Notification.TitleFont = New Font(Notification.TitleFont.Name, 10, FontStyle.Underline Or FontStyle.Bold)
            Notification.BodyColor = Color.LightGray
            Notification.GradientPower = 100

            'Conditional Settings
            If Severity = "Warning" Then
                'Warning popup
                Notification.TitleText = "LITENODES WARNING"
                Notification.Image = My.Resources.Yellow
            ElseIf Severity = "Error" Then
                'Error popup
                Notification.TitleText = "LITENODES ERROR"
                Notification.Image = My.Resources.Red
            Else
                'Anything else
                Notification.TitleText = "LITENODES"
                Notification.Image = My.Resources.Grey
            End If

            'Display Popup
            Notification.Popup()

        Catch
            sslError.Text = "There has been a critical error in the Windows notification display"
            sslError.BackColor = Color.Red
            sslError.ForeColor = Color.White
        End Try

        Return True

    End Function

    Private Function Log_Notification(Severity As String, Message As String, Optional ex As Exception = Nothing) As Boolean

        Try

            Dim LogEntry As String

            'Check if ex is supplied. If it is, it must be debug log

            If ex Is Nothing Then
                'Normal log entry

                'Pad out severity field for neat formatting of the log entry
                If Severity = "Warning" Then Severity = "Warning    "
                If Severity = "Error" Then Severity = "Error      "

                'Construct Log Entry
                LogEntry = Date.Now.ToString("dd/MM/yyyy HH:mm:ss.fff") + " | " + Severity + " | " + Message + Environment.NewLine

            Else
                'Debug log entry

                'Construct Log Entry
                LogEntry = "----------------------------------------------"
                LogEntry += Environment.NewLine
                LogEntry += Date.Now.ToString("dd/MM/yyyy HH:mm:ss.fff")
                LogEntry += Environment.NewLine
                LogEntry += "----------------------------------------------"
                LogEntry += Environment.NewLine
                LogEntry += "Human Message: " + Message
                LogEntry += Environment.NewLine
                LogEntry += "Exception Message: " + ex.Message
                LogEntry += Environment.NewLine
                LogEntry += "StackTrace: " + ex.StackTrace
                LogEntry += Environment.NewLine
                LogEntry += "Source: " + ex.Source
                LogEntry += Environment.NewLine
                LogEntry += "TargetSite: " + ex.TargetSite.ToString
                LogEntry += Environment.NewLine

            End If

            'Write entry to log
            If File.Exists(LogFileName) Then
                File.SetAttributes(LogFileName, FileAttributes.Normal)
            End If

            File.AppendAllText(LogFileName, LogEntry)
            File.SetAttributes(LogFileName, FileAttributes.ReadOnly)

        Catch
            sslError.Text = "There has been a critical error in the Log notification process"
            sslError.BackColor = Color.Red
            sslError.ForeColor = Color.White
        End Try

        Return True

    End Function

    Private Function Send_Email_Notification(Severity As String, Message As String) As Boolean

        Try
            'create the mail message
            Dim mail As New MailMessage()

            'set the addresses
            mail.From = New MailAddress("notifications@litenodes")
            mail.[To].Add(txtEmailAddress.Text)

            'set the content
            Dim Subject As String = "LiteNodes " + Severity
            mail.Subject = Subject
            mail.IsBodyHtml = False
            mail.Body = Message

            'set the server
            Dim smtp As New SmtpClient()
            smtp.Host = txtSMTPHost.Text
            smtp.UseDefaultCredentials = False
            smtp.Credentials = New NetworkCredential(txtSMTPUsername.Text, txtSMTPPassword.Text)
            smtp.Port = txtSMTPPort.Text
            smtp.EnableSsl = chkUseSSL.Checked

            'send the message
            smtp.Send(mail)

        Catch
            sslError.Text = "There has been an error sending an email. Please check email settings"
            sslError.BackColor = Color.Red
            sslError.ForeColor = Color.White
        End Try

        Return True

    End Function

    Private Sub timClearError_Tick(sender As Object, e As EventArgs) Handles timClearError.Tick

        'Clear application error message and turn timer off
        Clear_Application_Notification()
        timClearError.Enabled = False

    End Sub

    Private Sub chkAllowLogging_CheckedChanged(sender As Object, e As EventArgs) Handles chkAllowLogging.CheckedChanged

        Disable_Logging_Settings()

    End Sub

    Private Sub Disable_Logging_Settings()

        Try
            'Enable or disable the remaining logging controls
            If chkAllowLogging.Checked = True Then
                btnDisplayLog.Enabled = True
                btnClearLog.Enabled = True
                comLogLvl.Enabled = True
                btnCopyLog.Enabled = True
                Notification_Display("Information", "The logging controls have been enabled")
            Else
                btnDisplayLog.Enabled = False
                btnClearLog.Enabled = False
                comLogLvl.Enabled = False
                btnCopyLog.Enabled = False
                Notification_Display("Information", "The logging controls have been disabled")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error changing the logging controls availability", ex)
        End Try

    End Sub

    Private Sub chkApplicationNotification_CheckedChanged(sender As Object, e As EventArgs) Handles chkApplicationNotification.CheckedChanged

        Disable_Application_Notification_Settings()

    End Sub

    Private Sub Disable_Application_Notification_Settings()

        Try
            'Enable or disable the remaining application notification controls
            If chkApplicationNotification.Checked = True Then
                comAppNotifLvl.Enabled = True
                Notification_Display("Information", "The application notification controls have been enabled")
            Else
                comAppNotifLvl.Enabled = False
                Notification_Display("Information", "The application notification controls have been disabled")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error changing the application notification controls availability", ex)
        End Try

    End Sub

    Private Sub chkWindowsNotification_CheckedChanged(sender As Object, e As EventArgs) Handles chkWindowsNotification.CheckedChanged

        Disable_Windows_Notification_Settings()

    End Sub

    Private Sub Disable_Windows_Notification_Settings()

        Try
            'Enable or disable the remaining windows notification controls
            If chkWindowsNotification.Checked = True Then
                comWinNotifLvl.Enabled = True
                Notification_Display("Information", "The windows notification controls have been enabled")
            Else
                comWinNotifLvl.Enabled = False
                Notification_Display("Information", "The windows notification controls have been disabled")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error changing the windows notification controls availability", ex)
        End Try

    End Sub

    Private Sub chkAllowEmailNotification_CheckedChanged(sender As Object, e As EventArgs) Handles chkAllowEmailNotification.CheckedChanged

        Disable_Email_Notification_Settings()

    End Sub

    Private Sub Disable_Email_Notification_Settings()

        Try
            'Enable or disable the remaining email notification controls
            If chkAllowEmailNotification.Checked = True Then
                comEmailNotifLvl.Enabled = True
                txtSMTPHost.Enabled = True
                txtSMTPPort.Enabled = True
                txtSMTPUsername.Enabled = True
                txtSMTPPassword.Enabled = True
                chkUseSSL.Enabled = True
                txtEmailAddress.Enabled = True
                pbxShow.Enabled = True
                btnTestEmail.Enabled = True
                Notification_Display("Information", "The email notification controls have been enabled")
            Else
                comEmailNotifLvl.Enabled = False
                txtSMTPHost.Enabled = False
                txtSMTPPort.Enabled = False
                txtSMTPUsername.Enabled = False
                txtSMTPPassword.Enabled = False
                chkUseSSL.Enabled = False
                txtEmailAddress.Enabled = False
                pbxShow.Enabled = False
                btnTestEmail.Enabled = False
                Notification_Display("Information", "The email notification controls have been disabled")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error changing the email notification controls availability", ex)
        End Try

    End Sub

    Private Sub Display_Log()

        Try
            'Open notepad, and if log file exists, open it
            If File.Exists(LogFileName) = True Then
                Process.Start("notepad.exe", LogFileName)
            Else
                Process.Start("notepad.exe")
            End If

            Notification_Display("Information", "The log file has been opened successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error opening the log file", ex)
        End Try

    End Sub

    Private Sub btnDisplayLog_Click(sender As Object, e As EventArgs) Handles btnDisplayLog.Click

        Display_Log()

    End Sub

    Private Sub btnClearLog_Click(sender As Object, e As EventArgs) Handles btnClearLog.Click

        Try
            'Get confirmation
            If Request_Confirmation("This will permanently delete the logs") = True Then

                'If log file exists, then delete it
                If File.Exists(LogFileName) = True Then
                    File.SetAttributes(LogFileName, FileAttributes.Normal)
                    File.Delete(LogFileName)
                End If
            End If

            Notification_Display("Information", "The log file has been cleared")
        Catch ex As Exception
            Notification_Display("Error", "There was an error clearing the log file", ex)
        End Try

    End Sub

    Private Sub Populate_IP_Address_Details()

        Dim IP As String
        Dim parseIP As JObject 'JSON Object to hold API data

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

        'Load the data from the API into a JSON object
        Try
            Notification_Display("Information", "The API download from ip-api has started")
            IP = New WebDownload(2000).DownloadString("http://ip-api.com/json/" + txtIPAddress.Text + "?fields=country,region,city,zip,isp")
            Notification_Display("Information", "The API download from ip-api has completed successfully")
        Catch ex As Exception
            Notification_Display("Error", "IP API is unreachable. Please check network connection", ex)
            IP = ""
        End Try

        Try
            If IP <> "" Then
                parseIP = JObject.Parse(IP)

                'Populate the fields on the node status tab
                lblCountryNameValue.Text = parseIP.SelectToken("country").ToString
                lblRegionValue.Text = parseIP.SelectToken("region").ToString
                lblCityValue.Text = parseIP.SelectToken("city").ToString
                lblZipCodeValue.Text = parseIP.SelectToken("zip").ToString
                lblISPValue.Text = parseIP.SelectToken("isp").ToString
            Else
                'Clear the fields on the node status tab
                lblCountryNameValue.Text = ""
                lblRegionValue.Text = ""
                lblCityValue.Text = ""
                lblZipCodeValue.Text = ""
                lblISPValue.Text = ""
            End If

            Notification_Display("Information", "The IP address details have been displayed successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the IP address details", ex)
        End Try

    End Sub

    Private Sub Setup_Logging()

        Try
            Dim LogFileDirectory As String = "C:\Users\" + Environment.UserName + "\AppData\Local\LiteNodes"

            'Create logging path
            If Directory.Exists(LogFileDirectory) = False Then
                Directory.CreateDirectory(LogFileDirectory)
            End If

            LogFileName = LogFileDirectory + "\litenodes.log"
        Catch
            sslError.Text = "There has been a critical error in the logging configuration process"
            sslError.BackColor = Color.Red
            sslError.ForeColor = Color.White
        End Try

    End Sub

    Private Sub Populate_Node_Map()

        Try
            Dim IPAddress As String
            Dim Port As String
            Dim Location As String() = Nothing
            Dim Longitude As String
            Dim Latitude As String
            Dim NodePoint As GeoPoint

            'Clear all markers
            MapControl1.Markers.Clear()

            'Write a point onto the map for each node in the nodelist
            Dim RowCount As Integer = grdNodeList.RowCount
            If RowCount > 0 Then
                For Row As Integer = 0 To RowCount - 1
                    IPAddress = grdNodeList.Rows(Row).Cells(0).Value
                    Port = grdNodeList.Rows(Row).Cells(1).Value
                    Location = Lookup_IP_Location(IPAddress)
                    Longitude = Location(0)
                    Latitude = Location(1)
                    If Longitude <> "" And Latitude <> "" Then

                        'Set marker's location point
                        NodePoint.Longitude = Longitude
                        NodePoint.Latitude = Latitude

                        'Create marker instance specify Location On the map, Drawing style, And Label (Label not displayed but used when clicking on map)
                        Dim NodeMarker = New Marker(NodePoint, MarkerStyle.Default, IPAddress + "|" + Port)

                        'Add marker to the map
                        MapControl1.Markers.Add(NodeMarker)

                    End If
                Next
            End If

            'Highlight the currently selected node on the map. Icon shows whether its on or offline
            If chkHighlightCurrentNode.Checked = True Then
                IPAddress = txtIPAddress.Text
                If IPAddress <> "" And Validate_IPAddress(IPAddress) <> "Invalid" Then
                    Location = Lookup_IP_Location(IPAddress)
                    Longitude = Location(0)
                    Latitude = Location(1)
                    If Longitude <> "" And Latitude <> "" Then

                        'Set marker's location point
                        NodePoint.Longitude = Longitude
                        NodePoint.Latitude = Latitude

                        'Create marker instance specify Location On the map, Drawing style, And Label (Label used to flag this should be a highlight icon)
                        Dim NodeMarker = New Marker(NodePoint, MarkerStyle.Default, "Highlight")

                        'Add marker to the map
                        MapControl1.Markers.Add(NodeMarker)

                    End If
                End If
            End If

            Notification_Display("Information", "The node map has been created successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error creating the node map", ex)
        End Try

    End Sub

    Private Sub btnCopyLog_Click(sender As Object, e As EventArgs) Handles btnCopyLog.Click

        Try
            'Get path to users desktop
            Dim DesktopPath As String = "C:\Users\" + Environment.UserName + "\Desktop\LiteNodes.log"

            'Copy log to the users desktop
            If File.Exists(LogFileName) Then
                If File.Exists(DesktopPath) = True Then
                    File.Delete(DesktopPath)
                End If
                File.Copy(LogFileName, DesktopPath)
                File.SetAttributes(DesktopPath, FileAttributes.Normal)
                Notification_Display("Information", "Log file has been copied to desktop")
            Else
                Notification_Display("Warning", "Log file is currently empty so will not be copied")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error copying the log file to the desktop", ex)
        End Try

    End Sub

    Private Sub Set_Up_Map_Cache()

        Try
            'Set map location cache path and filename
            MapCacheFileName = "C:\Users\" + Environment.UserName + "\AppData\Local\LiteNodes\IP_Location.txt"

            'If cache file doesnt already exist, create it and write headers
            If File.Exists(MapCacheFileName) = False Then
                File.AppendAllText(MapCacheFileName, "IP Address,Longitude,Latitude" + Environment.NewLine)
            End If

            Notification_Display("Information", "The node map cache has been configured")
        Catch ex As Exception
            Notification_Display("Error", "There was an error configuring the node map cache", ex)
        End Try

    End Sub

    Private Sub Read_IP_Locations()

        Dim IPLocationLines As String()
        Dim IPLocationFields As String()

        Try
            'Read IP locations from text file to array
            IPLocationLines = File.ReadAllLines(MapCacheFileName)
            ReDim IPLocations(2, IPLocationLines.Length - 1)
            For i As Integer = 0 To IPLocationLines.Length - 1
                IPLocationFields = IPLocationLines(i).Split(",")
                IPLocations(0, i) = IPLocationFields(0)
                IPLocations(1, i) = IPLocationFields(1)
                IPLocations(2, i) = IPLocationFields(2)
            Next

            Notification_Display("Information", "Map cache successfully read from file")
        Catch ex As Exception
            Notification_Display("Error", "There was an error reading the map cache from file", ex)
        End Try

    End Sub

    Private Sub Write_IP_Locations()

        Dim Row As String

        Try
            'Delete existing Text file
            If File.Exists(MapCacheFileName) Then
                File.Delete(MapCacheFileName)
            End If

            'Write new file from array
            For i As Integer = 0 To IPLocations.GetLength(1) - 1
                Row = IPLocations(0, i) + "," + IPLocations(1, i) + "," + IPLocations(2, i) + Environment.NewLine
                File.AppendAllText(MapCacheFileName, Row)
            Next

        Catch
            'skip normal notification methods as this is run in a separate thread
            MessageBox.Show("There was a problem writing the IP locations to a text file", "LiteNodes - Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try

    End Sub

    Private Function Lookup_IP_Location(IPAddress As String) As String()

        Dim Longitude As String = "Not Found"
        Dim Latitude As String = "Not Found"
        Dim Location(1) As String

        Try
            'Check if IPAddress is in Array
            For i As Integer = 0 To IPLocations.GetLength(1) - 1
                If IPLocations(0, i) = IPAddress Then
                    Longitude = IPLocations(1, i)
                    Latitude = IPLocations(2, i)
                    Exit For
                End If
            Next

            'If address not in array, then add it to the array with blank longitude and latitude values
            If Longitude = "Not Found" Or Latitude = "Not Found" Then
                Longitude = ""
                Latitude = ""
                Dim Length As Integer = IPLocations.GetLength(1)
                ReDim Preserve IPLocations(2, Length)
                IPLocations(0, Length) = IPAddress
                IPLocations(1, Length) = Longitude
                IPLocations(2, Length) = Latitude
            End If

            Location(0) = Longitude
            Location(1) = Latitude

            'No success notification as there would be too many
        Catch ex As Exception
            Notification_Display("Error", "There was an error retrieving the IP location for node " + IPAddress, ex)
        End Try

        Return Location

    End Function

    Private Sub Update_Cache()

        'Update the map cache when triggered by the timer every 3 seconds

        Dim parseIP As JObject 'JSON Object to hold API data
        Dim IPAddress As String = ""
        Dim Longitude As String
        Dim Latitude As String
        Dim CacheIndex As Integer
        Dim Percentage As Integer

        Try

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

            'Display total nodes in cache in the settings page
            lblCacheNodesValue.Text = (IPLocations.GetLength(1) - 1).ToString

            'Get first address in the array that has a blank location
            For i As Integer = 0 To IPLocations.GetLength(1) - 1
                CacheIndex = i
                If IPLocations(1, i) = "" Or IPLocations(2, i) = "" Then
                    IPAddress = IPLocations(0, i)
                    Exit For
                End If
            Next

            'Display percentage of nodes populated in the settings page
            If IPLocations.GetLength(1) = 1 Then
                'Only the header row
                Percentage = 0
            Else
                If IPAddress = "" Then CacheIndex += 1 'Correction for fully populated cache
                Percentage = 100 * (CacheIndex - 1) / (IPLocations.GetLength(1) - 1)
                lblPercentageNodesValue.Text = Percentage.ToString + "%"
            End If

            'Display progress underneath the map if it is a long way behind (e.g. after cache clear or first installation)
            If Percentage < 98 Then
                lblMapUpdate.Text = "The map is currently updating - " + Percentage.ToString + "% complete - approx " + (Convert.ToInt32((100 - Percentage) / 2)).ToString + " minutes to go"
                'Refresh the map every 30 seconds
                MapRefreshCounter += 1
                If MapRefreshCounter = 10 Then
                    MapRefreshCounter = 0
                    Populate_Node_Map()
                End If
            Else
                lblMapUpdate.Text = ""
            End If

            If IPAddress <> "" Then

                Dim IP As String

                'Load the data from the API for the IPAddress found
                Try
                    'No information logging here as the call may be made 20 times a minute!!
                    IP = New WebDownload(2000).DownloadString("http://ip-api.com/json/" + IPAddress + "?fields=lat,lon")
                Catch ex As Exception
                    Notification_Display("Error", "IP API is unreachable. Please check network connection", ex)
                    'extend interval to 1 minute to avoid unneccessary api calls
                    timUpdateCache.Enabled = False
                    timUpdateCache.Interval = 60000
                    timUpdateCache.Enabled = True
                    Exit Sub
                End Try

                'Restore default interval of 3 secs if no issues contacting API
                timUpdateCache.Enabled = False
                timUpdateCache.Interval = 3000
                timUpdateCache.Enabled = True

                parseIP = JObject.Parse(IP)
                Longitude = parseIP.SelectToken("lon")
                Latitude = parseIP.SelectToken("lat")

                'Save the data back into the cache
                IPLocations(1, CacheIndex) = Longitude
                IPLocations(2, CacheIndex) = Latitude

            Else

                'No need to add location data to an address, so remove first line from array instead to force gradual refresh
                'We want to remove one line per minute so need counter to limit rate
                CacheCounter += 1
                If CacheCounter = 20 Then
                    CacheCounter = 0
                    Remove_First_Row_From_Cache()
                End If

            End If

            'No success notification as there would be one every three seconds
        Catch ex As Exception
            Notification_Display("Error", "There was an error updating the cache", ex)
        End Try

    End Sub

    Private Sub timUpdateCache_Tick(sender As Object, e As EventArgs) Handles timUpdateCache.Tick

        Update_Cache()

    End Sub

    Private Sub Remove_First_Row_From_Cache()

        Try
            Dim Length As Integer = IPLocations.GetLength(1)

            If Length > 2 Then
                'Remove the second row (First is header) from cache by copying all rows up one place, and then rem dim to one line less
                For i As Integer = 1 To Length - 2
                    IPLocations(0, i) = IPLocations(0, i + 1)
                    IPLocations(1, i) = IPLocations(1, i + 1)
                    IPLocations(2, i) = IPLocations(2, i + 1)
                Next
                ReDim Preserve IPLocations(2, Length - 2)
                Notification_Display("Information", "First row removed from cache")
            Else
                Notification_Display("Information", "No rows to remove from cache from cache")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error removing the first row from the cache", ex)
        End Try

    End Sub

    Private Sub pbxShow_Click(sender As Object, e As EventArgs) Handles pbxShow.Click

        Try
            'Show or hide the SMTP password
            If txtSMTPPassword.PasswordChar = "*" Then
                txtSMTPPassword.PasswordChar = ""
                pbxShow.Image = My.Resources.eye_blind_icon
                Notification_Display("Information", "SMTP Password shown")
            Else
                txtSMTPPassword.PasswordChar = "*"
                pbxShow.Image = My.Resources.eye_icon
                Notification_Display("Information", "SMTP password hidden")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error updating the SMTP password visibility", ex)
        End Try

    End Sub

    Private Function Request_Confirmation(Message As String) As Boolean

        Dim Response As Boolean = False

        Try
            Dim Answer As DialogResult = MessageBox.Show(Message + " - Are you sure?", "LiteNodes", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2)

            If Answer = DialogResult.Yes Then
                Response = True
                Notification_Display("Information", "Request confirmed as Yes for message (" + Message + " - Are you sure?)")
            Else
                Response = False
                Notification_Display("Information", "Request confirmed as No for message (" + Message + " - Are you sure?)")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error in processing the request confirmation for message (" + Message + " - Are you sure?)", ex)
        End Try

        Return Response

    End Function

    Private Sub Read_JSON_String()

        Try
            'Read json from text file to global variable
            If File.Exists(JSONFileName) Then
                json = File.ReadAllText(JSONFileName)
                Notification_Display("Information", "JSON string successfully read from file")
            Else
                json = ""
                Notification_Display("Information", "JSON string was not read from file as file does not exist")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error reading the JSON string from file", ex)
        End Try

    End Sub

    Private Sub Write_JSON_String()

        Try
            'Delete existing Text file
            If File.Exists(JSONFileName) Then
                File.Delete(JSONFileName)
            End If

            'Write new file from json Global variable
            File.AppendAllText(JSONFileName, json)

            Notification_Display("Information", "JSON string successfully written to file")
        Catch ex As Exception
            Notification_Display("Error", "There was an error writing the JSON string to file", ex)
        End Try

    End Sub

    Private Sub Set_Up_JSON_Persistence()

        Try
            'Set JSON file path and filename
            JSONFileName = "C:\Users\" + Environment.UserName + "\AppData\Local\LiteNodes\JSON.txt"

            Notification_Display("Information", "JSON string persistence successfully configured")
        Catch ex As Exception
            Notification_Display("Error", "There was an error configuring the JSON string persistence", ex)
        End Try

    End Sub

    Private Sub btnClearLocationCache_Click(sender As Object, e As EventArgs) Handles btnClearLocationCache.Click

        Try
            'Clear the map cache
            If Request_Confirmation("If you clear the cache, it will take approximately 1 hour to rebuild itself") = True Then

                'Clear the cache keeping just the header row
                ReDim Preserve IPLocations(2, 0)

                'Repopulate map which will remove all points, but also add the IP addresses back into the cache
                Populate_Node_Map()

                Notification_Display("Information", "The map cache has been successfully cleared")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error clearing the map cache", ex)
        End Try

    End Sub

    Private Sub Create_ShortCut(ShortCutPath As String)

        Try
            Dim oShell As Object
            Dim oLink As Object

            'Create a shortcut to the current application in the designated path
            oShell = CreateObject("WScript.Shell")
            oLink = oShell.CreateShortcut(ShortCutPath + "\LiteNodes.lnk")
            oLink.TargetPath = Application.ExecutablePath
            oLink.WindowStyle = 1
            oLink.Save()

            Notification_Display("Information", "Shortcut has been created in (" + ShortCutPath + ")")
        Catch ex As Exception
            Notification_Display("Error", "There was an error creating the shortcut", ex)
        End Try

    End Sub

    Private Sub Delete_Shortcut(ShortCutPath As String)

        Try
            'Delete the shortcut to the current application in the designated path
            If File.Exists(ShortCutPath + "\LiteNodes.lnk") Then
                File.Delete(ShortCutPath + "\LiteNodes.lnk")
                Notification_Display("Information", "Shortcut has been deleted from (" + ShortCutPath + ")")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error deleting the shortcut", ex)
        End Try

    End Sub

    Private Sub Configure_Desktop_Shortcut()

        Try
            'Get path to users desktop
            Dim DesktopPath As String = "C:\Users\" + Environment.UserName + "\Desktop"

            'Add or remove the desktop shortcut
            If chkDesktopShortcut.Checked = True Then
                Create_ShortCut(DesktopPath)
            Else
                Delete_Shortcut(DesktopPath)
            End If

            Notification_Display("Information", "Desktop shortcut has been successfully configured")
        Catch ex As Exception
            Notification_Display("Error", "There was an error configuring the desktop shortcut", ex)
        End Try

    End Sub

    Private Sub Configure_Startup_Shortcut()

        Try
            'Get path to windows startup folder
            Dim StartupPath As String = Environment.GetFolderPath(Environment.SpecialFolder.Startup)

            'Add or remove the desktop shortcut
            If chkStartWithWindows.Checked = True Then
                Create_ShortCut(StartupPath)
            Else
                Delete_Shortcut(StartupPath)
            End If

            Notification_Display("Information", "Startup shortcut has been successfully configured")
        Catch ex As Exception
            Notification_Display("Error", "There was an error configuring the startup shortcut", ex)
        End Try

    End Sub

    Private Sub chkDesktopShortcut_CheckedChanged(sender As Object, e As EventArgs) Handles chkDesktopShortcut.CheckedChanged

        Configure_Desktop_Shortcut()

    End Sub

    Private Sub chkStartWithWindows_CheckedChanged(sender As Object, e As EventArgs) Handles chkStartWithWindows.CheckedChanged

        Configure_Startup_Shortcut()

    End Sub

    Private Sub comLogLvl_SelectedIndexChanged(sender As Object, e As EventArgs) Handles comLogLvl.SelectedIndexChanged

        Try
            'Request confirmation if level of "Everything" is selected for logging
            If comLogLvl.Text = "Everything" And FormLoadComplete = True Then
                If Request_Confirmation("This will cause rapid log growth and may slow down the application") = False Then
                    comLogLvl.Text = "Warning and Error"
                End If
                Notification_Display("Information", "Confirmation recieved to select 'everything' for logging")
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error requesting confirmation to select 'everything' for logging", ex)
        End Try

    End Sub

    Private Sub trkGreenToYellow_Scroll(sender As Object, e As EventArgs) Handles trkGreenToYellow.Scroll

        Try
            'Ensure Green to Yellow less than Yellow to Red
            If trkGreenToYellow.Value >= trkYellowToRed.Value Then
                trkGreenToYellow.Value = trkYellowToRed.Value - 1
            End If

            lblGreenToYellow.Text = trkGreenToYellow.Value

        Catch ex As Exception
            Notification_Display("Error", "There was an error changing the green to yellow threshold", ex)
        End Try

    End Sub

    Private Sub trkYellowToRed_Scroll(sender As Object, e As EventArgs) Handles trkYellowToRed.Scroll

        Try
            'Ensure Yellow to Red greater than Green to Yellow
            If trkYellowToRed.Value <= trkGreenToYellow.Value Then
                trkYellowToRed.Value = trkGreenToYellow.Value + 1
            End If

            lblYellowToRed.Text = trkYellowToRed.Value

        Catch ex As Exception
            Notification_Display("Error", "There was an error changing the yellow to red threshold", ex)
        End Try

    End Sub

    Private Sub Configure_Tooltips()

        Try
            'Set up the tooltips
            If chkShowTooltips.Checked = True Then
                ToolTip1.Active = True
                StatusStrip1.ShowItemToolTips = True
            Else
                ToolTip1.Active = False
                StatusStrip1.ShowItemToolTips = False
            End If

            Notification_Display("Information", "Tooltips have been successfully configured")
        Catch ex As Exception
            Notification_Display("Error", "There was an error configuring the tooltips", ex)
        End Try

    End Sub

    Private Sub chkShowTooltips_CheckedChanged(sender As Object, e As EventArgs) Handles chkShowTooltips.CheckedChanged

        Configure_Tooltips()

    End Sub

    Private Sub btnTestEmail_Click(sender As Object, e As EventArgs) Handles btnTestEmail.Click

        'Send a test email
        Send_Email_Notification("Test Email", "You have correctly configured your email settings")

    End Sub

    Private Sub chkHighlightCurrentNode_CheckedChanged(sender As Object, e As EventArgs) Handles chkHighlightCurrentNode.CheckedChanged

        're-populate the node map to hide or show the currently selected node. Only do this if user action
        If FormLoadComplete = True Then
            Populate_Node_Map()
        End If

    End Sub

    Private Sub LiteNodes_Version()

        'Check for updates to litenodes

        Dim jsonVersion As String
        Dim client As New WebDownload(2000)

        Try
            'Set default values in case github update cannot be obtained
            lblInstalledVersionValue.Text = My.Settings.LiteNodesVersion
            lblLatestVersionValue.Text = My.Settings.LiteNodesVersion
            lblUpdateStatus.Text = "Your current version of LiteNodes is up to date"
            btnUpdateNow.Enabled = False

            'Retrieve latest version from github

            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12
            client.Headers.Add("user-agent", "request")

            Try
                Notification_Display("Information", "The API download of latest LiteNodes version from github has started")
                jsonVersion = client.DownloadString("https://api.github.com/repos/starglancer/litenodes/releases/latest")
                Notification_Display("Information", "The API download of latest LiteNodes version from github has completed successfully")
            Catch ex As Exception
                Notification_Display("Error", "Github API is unreachable. Please check network connection", ex)
                'Skip the rest of the subroutine if github API unreachable
                Exit Sub
            End Try

            Dim parseVersion As JObject = JObject.Parse(jsonVersion)
            Dim Version As String = parseVersion.SelectToken("tag_name").ToString

            lblLatestVersionValue.Text = Version

            If lblLatestVersionValue.Text <> lblInstalledVersionValue.Text Then
                lblUpdateStatus.Text = "There is an updated version of LiteNodes available"
                btnUpdateNow.Enabled = True
            End If

            Notification_Display("Information", "The latest LiteNodes version has been successfully identified as " + Version)
        Catch ex As Exception
            Notification_Display("Error", "There was an error identifying the latest LiteNodes version. It will be assumed to be " + My.Settings.LiteNodesVersion, ex)
        End Try

    End Sub

    Private Sub btnCheckForUpdate_Click(sender As Object, e As EventArgs) Handles btnCheckForUpdate.Click

        LiteNodes_Version()

    End Sub

    Private Sub btnUpdateNow_Click(sender As Object, e As EventArgs) Handles btnUpdateNow.Click

        'Go to download link for the latest version
        Process.Start(My.Settings.DownLoadURL)

    End Sub

    Private Sub btnDefaultPort_Click(sender As Object, e As EventArgs) Handles btnDefaultPort.Click

        'Set Port to default Litecoin Core port
        txtPort.Text = My.Settings.DefaultPort

    End Sub

    Private Sub btnMyIPAddress_Click(sender As Object, e As EventArgs) Handles btnMyIPAddress.Click

        'Lookup my current IP address and display it in the box

        Dim IP As String
        Dim parseIP As JObject 'JSON Object to hold API data

        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12

        'Load the data from the API into a JSON object
        Try
            Notification_Display("Information", "The API download from ip-api for my IP address has started")
            IP = New WebDownload(2000).DownloadString("http://ip-api.com/json/?fields=query")
            Notification_Display("Information", "The API download from ip-api for my IP address has completed successfully")
        Catch ex As Exception
            Notification_Display("Error", "IP API is unreachable. Please check network connection", ex)
            IP = ""
        End Try

        Try
            If IP <> "" Then
                parseIP = JObject.Parse(IP)
                txtIPAddress.Text = parseIP.SelectToken("query").ToString
                Notification_Display("Information", "My current IP address has been displayed successfully as " + txtIPAddress.Text)
            End If
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying my current IP address", ex)
        End Try

    End Sub

    Private Sub timTextbox_Tick(sender As Object, e As EventArgs) Handles timTextbox.Tick

        'Refresh the node status tab and disable the timer to prevent it firing again
        Populate_NodeStatusTab()
        timTextbox.Enabled = False

    End Sub

    Private Sub Create_Port_Array()

        'Create an array of port statistics for the statistics grid and the port dropdown. This is unsorted as cannot sort a 2 dimensional array

        'Local Variables
        Dim Token As String()
        Dim Node As String()
        Dim PortString As String
        Dim Port As Integer
        Dim Found As Boolean
        Dim Length As Integer

        Try
            Dim parsenodes As JObject = parsejson.SelectToken("data.nodes")
            Dim nodes As List(Of JToken) = parsenodes.Children().ToList()

            'Clear previous array contents
            ReDim PortArray(1, 0)

            'Cycle through each node, and add its port to the array
            For Each JToken In nodes

                'Current length of array
                Length = PortArray.GetLength(1)

                'Get the port number for the node
                Token = JToken.ToString.Split(New Char() {""""c})
                Node = Token(1).Split(New Char() {":"c})
                PortString = Node(Node.Length - 1)
                PortString = PortString.Trim("""")
                Port = Convert.ToInt32(PortString)

                'Check if port is already in array, and if so, increase its count
                Found = False

                For i As Integer = 0 To Length - 1
                    If PortArray(0, i) = Port Then
                        PortArray(1, i) += 1
                        Found = True
                        Exit For
                    End If
                Next

                'If not already in array, then add it
                If Found = False Then
                    PortArray(0, Length - 1) = Port
                    PortArray(1, Length - 1) = 1
                    ReDim Preserve PortArray(1, Length)
                End If

            Next

            Notification_Display("Information", "The port array was created successfully with " + (Length - 1).ToString + " rows")

        Catch ex As Exception
            Notification_Display("Error", "There was an error in creating the port array", ex)
        End Try

    End Sub

    Private Sub btnRunLitecoinNode_Click(sender As Object, e As EventArgs) Handles btnRunLitecoinNode.Click

        'Go to link to running your own node
        Process.Start(My.Settings.LitecoinCoreURL)

    End Sub

    Private Sub btnWebsiteHelp_Click(sender As Object, e As EventArgs) Handles btnWebsiteHelp.Click

        'Open website in the default browser

        Try
            Process.Start(My.Settings.WebHelp)

            Notification_Display("Information", "The help website was opened successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error opening the help website", ex)
        End Try

    End Sub

    Private Sub btnEmailHelp_Click(sender As Object, e As EventArgs) Handles btnEmailHelp.Click

        'Send an email help request using the default email application

        Try
            Process.Start("mailto:" + My.Settings.EmailHelp + "?subject=LiteNodes%20Help%20Required")

            Notification_Display("Information", "Email help request was sent successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error sending the email help request", ex)
        End Try

    End Sub

    Private Sub btnInlineHelp_Click(sender As Object, e As EventArgs) Handles btnInlineHelp.Click

        'Open help file

        Try
            Process.Start(Application.StartupPath + "/LiteNodes.chm")

            Notification_Display("Information", "Help file was opened successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error opening the help file", ex)
        End Try

    End Sub

    Private Sub NotifyIcon1_MouseClick(sender As Object, e As MouseEventArgs) Handles NotifyIcon1.MouseClick

        Try
            'Only handle left mouse button click
            If e.Button = MouseButtons.Left Then
                'Restore window if minimised or Minimise Window if normal
                If WindowState = FormWindowState.Minimized Then
                    WindowState = FormWindowState.Normal
                ElseIf WindowState = FormWindowState.Normal Then
                    WindowState = FormWindowState.Minimized
                End If
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error resizing the application", ex)
        End Try

    End Sub

    Private Sub mnuSummary_Click(sender As Object, e As EventArgs) Handles mnuSummary.Click

        'Restore window to summary tab from tray icon menu

        Try
            TabControl1.SelectedTab = tabSummary
            WindowState = FormWindowState.Normal

            Notification_Display("Information", "Summary tab displayed successfully from tray icon menu")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the summary tab from the tray icon menu", ex)
        End Try

    End Sub

    Private Sub mnuStatistics_Click(sender As Object, e As EventArgs) Handles mnuStatistics.Click

        'Restore window to statistics tab from tray icon menu

        Try
            TabControl1.SelectedTab = tabStatistics
            WindowState = FormWindowState.Normal

            Notification_Display("Information", "Statistics tab displayed successfully from tray icon menu")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the statistics tab from the tray icon menu", ex)
        End Try

    End Sub

    Private Sub mnuNodeList_Click(sender As Object, e As EventArgs) Handles mnuNodeList.Click

        'Restore window to node list tab from tray icon menu

        Try
            TabControl1.SelectedTab = tabNodeList
            WindowState = FormWindowState.Normal

            Notification_Display("Information", "Node list tab displayed successfully from tray icon menu")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the node list tab from the tray icon menu", ex)
        End Try

    End Sub

    Private Sub mnuNodeMap_Click(sender As Object, e As EventArgs) Handles mnuNodeMap.Click

        'Restore window to node map tab from tray icon menu

        Try
            TabControl1.SelectedTab = tabNodeMap
            WindowState = FormWindowState.Normal

            Notification_Display("Information", "Node map tab displayed successfully from tray icon menu")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the node map tab from the tray icon menu", ex)
        End Try

    End Sub

    Private Sub mnuNodeStatus_Click(sender As Object, e As EventArgs) Handles mnuNodeStatus.Click

        'Restore window to node status tab from tray icon menu

        Try
            TabControl1.SelectedTab = tabNodestatus
            gbxStatus.Focus()
            WindowState = FormWindowState.Normal

            Notification_Display("Information", "Node status tab displayed successfully from tray icon menu")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the node status tab from the tray icon menu", ex)
        End Try

    End Sub

    Private Sub mnuSettings_Click(sender As Object, e As EventArgs) Handles mnuSettings.Click

        'Restore window to settings tab from tray icon menu

        Try
            TabControl1.SelectedTab = tabSettings
            WindowState = FormWindowState.Normal

            Notification_Display("Information", "Settings tab displayed successfully from tray icon menu")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the settings tab from the tray icon menu", ex)
        End Try

    End Sub

    Private Sub mnuHelp_Click(sender As Object, e As EventArgs) Handles mnuHelp.Click

        'Restore window to help tab from tray icon menu

        Try
            TabControl1.SelectedTab = tabHelp
            WindowState = FormWindowState.Normal

            Notification_Display("Information", "Help tab displayed successfully from tray icon menu")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the help tab from the tray icon menu", ex)
        End Try

    End Sub

    Private Sub mnuDisplayLog_Click(sender As Object, e As EventArgs) Handles mnuDisplayLog.Click

        Display_Log()

    End Sub

    Private Sub mnuExit_Click(sender As Object, e As EventArgs) Handles mnuExit.Click

        Try
            'Get confirmation before closing
            If Request_Confirmation("This will close the LiteNodes application") = True Then
                'Close application even if minimise on close is selected
                ForceCloseFlag = True
                Me.Close()
            End If

        Catch ex As Exception
            Notification_Display("Error", "There was an error closing the application", ex)
        End Try

    End Sub

    Private Sub TrayMenuStrip_Opening(sender As Object, e As CancelEventArgs) Handles TrayMenuStrip.Opening

        'When displaying the tray icon menu, hide the option to display the log file if logging is disabled in settings
        Try
            If chkAllowLogging.Checked = True Then
                mnuDisplayLog.Visible = True
                ToolStripSeparator1.Visible = True
            Else
                mnuDisplayLog.Visible = False
                ToolStripSeparator1.Visible = False
            End If

            Notification_Display("Information", "Tray icon menu displayed successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error displaying the tray icon menu", ex)
        End Try

    End Sub

    Private Sub Configure_Map_Control()

        Try
            'Configure the map control on the node map tab
            MapControl1.CacheFolder = "C:\Users\" + Environment.UserName + "\AppData\Local\LiteNodes\MapControl"
            MapControl1.TileServer = New OpenStreetMapTileServer("starglancer/litenodes/" + My.Computer.Name)
            MapControl1.MinZoomLevel = 1
            MapControl1.MaxZoomLevel = 10
            MapControl1.ZoomLevel = 1
            MapControl1.FitToBounds = True
            MapControl1.ShowThumbnails = True


            'Set image used for each node
            imageMarker = My.Resources.LitecoinFlat

            Notification_Display("Information", "The Node Map has been configured successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error configuring the node map", ex)
        End Try

    End Sub

    Private Sub MapControl1_DrawMarker(sender As Object, e As DrawMarkerEventArgs) Handles MapControl1.DrawMarker

        'This intercepts the draw marker process to force the drawing of an image instead
        Try
            'Set flag To override drawing of default marker
            e.Handled = True

            'Set scale to draw the image at (larger image for higher zoom levels)
            Dim Scale As Integer = 4 + MapControl1.ZoomLevel * 2

            'Draw image
            If e.Marker.Label = "Highlight" Then
                'Highlight flag set, so this is the currently selected node in the node status tab
                e.Graphics.DrawImage(pbxStatus.Image, New Rectangle(e.Point.X - 6 - Scale / 4, e.Point.Y - 6 - Scale / 4, 12 + Scale / 2, 12 + Scale / 2))
            Else
                'Normal node
                e.Graphics.DrawImage(imageMarker, New Rectangle(e.Point.X - Scale / 2, e.Point.Y - Scale / 2, Scale, Scale))
            End If

            'No success message as this routine is called almost 1000 times every time the map is refreshed
        Catch ex As Exception
            Notification_Display("Error", "There was an error drawing the node on the map", ex)
        End Try

    End Sub

    Private Sub btnClearMapCache_Click(sender As Object, e As EventArgs) Handles btnClearMapCache.Click

        Try
            Dim MapCacheDirectory As String = "C:\Users\" + Environment.UserName + "\AppData\Local\LiteNodes\MapControl"

            'Get confirmation
            If Request_Confirmation("This will impair performance whilst the cache rebuilds") = True Then

                'If map cache directory exists, then delete it recursively
                If Directory.Exists(MapCacheDirectory) = True Then
                    Directory.Delete(MapCacheDirectory, True)
                End If
            End If

            Notification_Display("Information", "The Map Cache has been cleared successfully")
        Catch
            'Ignore any errors as some files may not be deleted if in use. This is expected
            Notification_Display("Information", "The Map Cache has been cleared, but some files could not be deleted")
        Finally
            'Update display of map cache size
            Check_Map_Cache_size()
        End Try

    End Sub

    Private Sub btnCheckMapCacheSize_Click(sender As Object, e As EventArgs) Handles btnCheckMapCacheSize.Click

        Check_Map_Cache_size()

    End Sub

    Private Sub Check_Map_Cache_size()

        Try
            Dim MapCacheDirectory As String = "C:\Users\" + Environment.UserName + "\AppData\Local\LiteNodes\MapControl"

            If Directory.Exists(MapCacheDirectory) Then
                Dim DirInfo As DirectoryInfo = New DirectoryInfo(MapCacheDirectory)
                Dim Size As String = GetDirectoryFileSize(DirInfo).ToString
                lblMapCacheSize.Text = Format(Size / 1024 / 1024, "###,0") & " MB"
            Else
                lblMapCacheSize.Text = "0 MB"
            End If

            Notification_Display("Information", "The map cache size has been updated successfully")
        Catch ex As Exception
            Notification_Display("Error", "There was an error updating the map cache size", ex)
        End Try

    End Sub

    Private Function GetDirectoryFileSize(DirectoryInfo As DirectoryInfo) As Long

        'Recursive function to get size of directory and all sub directories

        Dim TotalSize As Long = 0

        Try
            'Add file-size in a parent folder
            For Each FileInfo As FileInfo In DirectoryInfo.GetFiles()
                TotalSize += FileInfo.Length
            Next FileInfo

            'Add file-size in a Sub-Folder (Recursive)
            For Each DirInfo As DirectoryInfo In DirectoryInfo.GetDirectories()
                TotalSize += GetDirectoryFileSize(DirInfo)
            Next DirInfo

            'No success message as its a recursive function and it would generate too many log entries
        Catch ex As Exception
            Notification_Display("Error", "There was an error calculating the map cache size", ex)
        End Try

        'Return total size
        Return TotalSize

    End Function

    Private Sub MapControl1_MouseClick(sender As Object, e As MouseEventArgs) Handles MapControl1.MouseClick

        'If user right clicks on map it will find the nearest node and display its details on the node status tab

        Dim ClickPoint As GeoPoint
        Dim ClickLongitude As Single
        Dim ClickLatitude As Single
        Dim Point As GeoPoint
        Dim Longitude As Single
        Dim Latitude As Single
        Dim LongDiff As Single
        Dim LatDiff As Single
        Dim TotalDiff As Single
        Dim SmallestTotalDiff As Single
        Dim IPAddress As String
        Dim Port As String
        Dim Label As String = ""
        Dim Details() As String

        Try
            'Ignore a left mouse button click
            If e.Button = MouseButtons.Right Then

                'Get longitude and latitude where the mouse has been right clicked
                ClickPoint = MapControl1.Mouse
                ClickLongitude = ClickPoint.Longitude
                ClickLatitude = ClickPoint.Latitude

                'Check for nearest marker and get its label
                SmallestTotalDiff = 1000
                For Each Nodemarker As Marker In MapControl1.Markers
                    Point = Nodemarker.Point
                    Longitude = Point.Longitude
                    Latitude = Point.Latitude
                    LongDiff = Math.Abs(ClickLongitude - Longitude)
                    LatDiff = Math.Abs(ClickLatitude - Latitude)
                    TotalDiff = LongDiff + LatDiff
                    If TotalDiff < SmallestTotalDiff Then
                        SmallestTotalDiff = TotalDiff
                        Label = Nodemarker.Label
                    End If
                Next

                'Get details from label
                If Label <> "" Then
                    Details = Split(Label, "|")
                    IPAddress = Details(0)
                    Port = Details(1)
                Else
                    IPAddress = ""
                    Port = ""
                End If

                'Set as current node on status tab
                txtIPAddress.Text = IPAddress
                txtPort.Text = Port

                'Refresh details on node status tab
                Populate_NodeStatusTab()

                'Switch to Node Status Tab and move focus away from IPAddress text box
                TabControl1.SelectedTab = tabNodestatus
                gbxStatus.Focus()

            End If

            Notification_Display("Information", "Node " + Label + " has been successfully selected from the map")
        Catch ex As Exception
            Notification_Display("Error", "There was an error selecting a node from the map", ex)
        End Try

    End Sub

    Private Sub Recreate_Map_Control()

        'Required as a kludge to overcome issue with high CPU usage of mapcontrol component
        'It destroys and recreates the map control, killing off any runaway processes

        Try
            'Destroy old control
            MapControl1.Dispose()

            'Create new control
            MapControl1 = New MapControl With {
                .Location = New Point(37, 21),
                .Size = New Size(720, 360)
            }

            'Add to map tab
            tabNodeMap.Controls.Add(MapControl1)

            'Configure it
            Configure_Map_Control()

            'Populate it
            Populate_Node_Map()

            Notification_Display("Information", "The map control has been successfully recreated")
        Catch ex As Exception
            Notification_Display("Error", "There was an error recreating the map control", ex)
        End Try

    End Sub

    Private Sub tabNodeMap_Leave(sender As Object, e As EventArgs) Handles tabNodeMap.Leave

        Recreate_Map_Control()

    End Sub

End Class
