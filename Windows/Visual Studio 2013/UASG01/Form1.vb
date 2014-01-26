Imports System.Collections.ObjectModel
Imports System.Text
Imports System.Globalization
Imports System.IO.Ports

Public Class frmMain
    Public Delegate Sub DeclareCheapGuiAccess(C As Control, T As String)
    Public Sub DoCheapGuiAccess(C As Control, T As String)
        C.Text = T
    End Sub
    Private FctCatalogue As XElement =
        <FctCat>
            <Fct id="0x05" name="DateAndTime">
                <Text xml:lang="de">Datum und Uhrzeit</Text>
                <Data>
                    <Prop name="Year" type="uint8" minD="0" maxD="99"/>
                    <Prop name="Month" type="uint4" minD="1" maxD="12"/>
                    <Prop name="Day" type="uint8" minD="1" maxD="31"/>
                    <Prop name="Hour" type="uint8" minD="0" maxD="23"/>
                    <Prop name="Minute" type="uint8" minD="0" maxD="59"/>
                    <!--Prop name="Second" type="uint8"/>
                    <Prop name="Details" type="uint4" bitfield="true">
                        <Bit name="DST"/>
                        <Enum name="DCF77" bits="2">
                            <Num value="0x0" name="no signal"/>
                            <Num value="0x1" name="learning"/>
                            <Num value="0x2" name="unsure"/>
                            <Num value="0x3" name="valid"/>
                        </Enum>
                        <Bit name="reserved1"/>
                    </Prop-->
                </Data>
            </Fct>
            <Fct id="0x04" name="seconds">
                <Text xml:lang="de">Sekunden</Text>
                <Data>
                    <Prop name="Second" type="uint8" minD="0" maxD="59"/>
                </Data>
            </Fct>
            <Fct id="0x06" name="MainsSwich">
                <Text xml:lang="de">Steckdosenschalter</Text>
                <Data>
                    <Prop name="Pump" type="uint4" bitfield="true">
                        <Bit name="FilterPump"/>
                        <Bit name="Heating"/>
                        <Bit name="Light"/>
                        <Bit name="AirPump"/>
                    </Prop>
                </Data>
            </Fct>
            <Fct id="0x07" name="PWM">
                <Text xml:lang="de">PWM</Text>
                <Data>
                    <Prop name="CH1" type="uint8" gui="TrackBar" minD="0" maxD="63"/>
                    <Prop name="CH2" type="uint8" gui="TrackBar" minD="0" maxD="63"/>
                    <Prop name="CH3" type="uint8" gui="TrackBar" minD="0" maxD="63"/>
                    <Prop name="CH4" type="uint8" gui="TrackBar" minD="0" maxD="63"/>
                    <Prop name="CH5" type="uint8" gui="TrackBar" minD="0" maxD="63"/>
                    <Prop name="CH6" type="uint8" gui="TrackBar" minD="0" maxD="63"/>
                </Data>
            </Fct>
        </FctCat>
    Private FCPFunctions As New FCPFunctionList

    Private Sub frmMain_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing
        If SerialPort1.IsOpen Then
            SerialPort1.Close()
        End If
    End Sub
    Private Sub SerialPort1_DataReceived(sender As Object, e As IO.Ports.SerialDataReceivedEventArgs) Handles SerialPort1.DataReceived
        Static Dim state As String = "init"
        Static Buffer As New Queue(Of Integer)
        Static Len As Integer = 0
        Static MsgBuffer As New StringBuilder
        Static Fct As FCPFunction = Nothing

        Dim ByteBuffer(1000) As Byte
        Dim ReadLen As Integer
        ReadLen = SerialPort1.Read(ByteBuffer, 0, 1000) 'Bescheuerte Programmierung, geht das nicht besser?
        For i As Integer = 0 To ReadLen - 1
            Buffer.Enqueue(ByteBuffer(i))
        Next
        Dim Processed As Boolean = False
        Do
            If state = "init" Then
                If Buffer.Count > 0 Then
                    Dim ReadCh As Integer
                    ReadCh = Buffer.Dequeue
                    If (ReadCh > &H50) AndAlso (ReadCh <= &H5F) Then 'start of Frame 1..16 Nibbles
                        Len = ReadCh - &H50
                        state = "WaitFctID"
                    Else
                        If ReadCh = 13 Then
                            ' CR ist obsolet, aber erlaubt
                        Else
                            Debug.Print("Zeichen 0x" & ReadCh.ToString("X"))
                        End If
                    End If
                Else : Processed = True : End If
            End If
            If state = "WaitFctID" Then
                If Buffer.Count > 0 Then
                    Dim FctCodeHex As String
                    FctCodeHex = ChrW(Buffer.Dequeue)
                    Dim FctCodeInt As Integer
                    If Integer.TryParse(FctCodeHex, NumberStyles.HexNumber, Nothing, FctCodeInt) Then
                        'Todo: bei 0: Zwei weitere Nibbles einlesen (16..271) und Len -= 2 'Erweiterte FctID abziehen
                        If FCPFunctions.Contains(FctCodeInt) Then
                            Fct = FCPFunctions.Item(FctCodeInt)
                            'Debug.Print("1 - Fct-ID " & Fct.Name)
                            'If Fct.FctID = 5 Then Stop
                            state = "Command"
                        Else
                            Debug.Print("Unbekannte Fct-ID " & FctCodeHex)
                            state = "init"
                        End If
                    Else
                        Debug.Print("WaitFctID no hex val: " & FctCodeHex)
                        state = "init"
                    End If
                Else : Processed = True : End If
            End If
            If state = "Command" Then
                If Buffer.Count > 0 Then
                    '0x01 = Status (StatusAck, Get, ChangedArray, ...)
                    Dim Cmd As String = Buffer.Dequeue
                    If ChrW(Cmd) = "1" Then
                        Len -= 1 'Länge Kommando abziehen
                        If Len = Fct.NibbleCount() Then
                            'Debug.Print("2 - Fct-ID " & Fct.Name)
                            state = "StatusGet"
                        Else
                            Debug.Print("Fct-ID " & Fct.Name & ": Falsche Länge, " & Len & " statt " & Fct.NibbleCount() & "!")
                            state = "init"
                        End If
                    Else
                        Debug.Print("Fct-ID " & Fct.Name & ": unbekanntes Kommando: " & Cmd)
                    End If
                Else : Processed = True : End If
            End If
            If state = "StatusGet" Then
                If Buffer.Count >= Len Then
                    If Fct.DeQueueData(Buffer) Then
                        'Debug.Print("3 - Fct-ID " & Fct.Name)
                    End If
                    state = "init"
                Else : Processed = True : End If
            End If
        Loop Until Processed
    End Sub

    Private Sub CbxComPort_DropDownClosed(sender As Object, e As EventArgs) Handles CbxComPort.DropDownClosed
        If Not CbxComPort.SelectedItem Is Nothing Then
            If SerialPort1.IsOpen Then SerialPort1.Close()
            If CbxComPort.SelectedItem.ToString.StartsWith("COM") Then
                SerialPort1.PortName = CbxComPort.SelectedItem.ToString
                SerialPort1.Open()
                Debug.Print(SerialPort1.PortName & " opened")
            Else
                SerialPort1.Close()
                Debug.Print("COM closed")
            End If
        Else
            SerialPort1.Close()
            Debug.Print("COM closed")
        End If
    End Sub

    Private Sub ToolStripButton1_Click(sender As Object, e As EventArgs) Handles ToolStripButton1.Click
        ToolStripButton1.Enabled = False
        Me.SuspendLayout()
        For Each Fct In FctCatalogue.<Fct>
            Dim NF As FCPFunction = FCPFunction.Create(Fct)
            FCPFunctions.Add(NF)
        Next
        Dim TLP As New TableLayoutPanel With {.Dock = DockStyle.Fill}
        With TLP
            .RowStyles.Clear()
            .ColumnStyles.Clear()
            .ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 60))
            .ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, 60))
            .ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60))
            .CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        End With
        TPgFCPControl.Controls.Add(TLP)
        Dim Row As Integer = 0
        For Each f As FCPFunction In FCPFunctions
            Dim FLP As New FlowLayoutPanel With {.AutoSize = True, .Dock = DockStyle.Fill, .Margin = New Padding(0)}
            Dim HL As New Label With {.Text = f.Name, .AutoSize = True, .Margin = New Padding(0, 4, 0, 0), .Font = New Font(TabControl1.Font.FontFamily, CSng(TabControl1.Font.Size * 1.4139999999999999), FontStyle.Bold)}
            Dim BtnSet As New Button With {.Text = "SetGet", .Tag = f}
            AddHandler BtnSet.Click, AddressOf ButtonSetGet_Click
            FLP.Controls.Add(HL)
            FLP.Controls.Add(BtnSet)
            TLP.SetColumnSpan(FLP, 3)
            TLP.Controls.Add(FLP, 0, Row)
            Row += 1
            For Each P As FCPProperty In f.Properties
                Dim NTBGet As New TextBox With {.Dock = DockStyle.Fill}
                TLP.Controls.Add(New Label() With {.Text = P.Name, .Margin = New Padding(3, 6, 0, 0)}, 0, Row)
                TLP.Controls.Add(NTBGet, 1, Row)
                P.ControlGet = NTBGet
                If P.XML.@gui = "TrackBar" Then
                    Dim NTB As New TrackBar With {.Dock = DockStyle.Fill, .Margin = New Padding(0)}
                    NTB.Minimum = P.minD
                    NTB.Maximum = P.maxD
                    NTB.Tag = P
                    'P.UISetValue(NTB.Value) 'Der Wert muss von der FU kommen
                    AddHandler NTB.Scroll, AddressOf TrackBars_Scroll
                    TLP.Controls.Add(NTB, 2, Row)
                    ToolTip.SetToolTip(NTB, "hier soll der Wert stehen")
                    P.ControlSet = NTB
                Else
                    Dim NTB As New TextBox With {.Dock = DockStyle.Fill}
                    TLP.Controls.Add(NTB, 2, Row)
                    P.ControlSet = NTB
                End If
                Row += 1
            Next
        Next
        Dim Ports() As String
        Ports = SerialPort.GetPortNames
        For Each S As String In Ports
            CbxComPort.Items.Add(S)
        Next
        CbxComPort.SelectedIndex = 0
        Me.ResumeLayout()
    End Sub

    Private Sub TrackBars_Scroll(sender As Object, e As EventArgs)
        Dim TB As TrackBar = TryCast(sender, TrackBar)
        If TB Is Nothing Then Stop
        Dim P As FCPProperty = TryCast(TB.Tag, FCPProperty)
        If P Is Nothing Then Stop
        P.UISetValue(TB.Value)
        'Debug.Print(TB.Value)
        ToolTip.SetToolTip(TB, TB.Value)
        If btnSyncPWM.Checked Then
            For Each P1 As FCPProperty In P.Function.Properties
                Dim TB1 As TrackBar = TryCast(P1.ControlSet, TrackBar)
                TB1.Value = TB.Value ' löst scroll nicht aus!
                P1.UISetValue(TB.Value)
            Next
        End If
    End Sub

    Private Sub Timer_Tick(sender As Object, e As EventArgs) Handles Timer.Tick
        Dim clean As Boolean
        Do
            clean = True
            For Each F As FCPFunction In FCPFunctions
                If F.IsDirty Then
                    clean = False
                    F.FCP_Set(SerialPort1)
                End If
            Next
        Loop Until clean
    End Sub

    Private Sub frmMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ToolStripButton1_Click(Nothing, Nothing)
    End Sub

    Private Sub ButtonSetGet_Click(sender As Object, e As EventArgs)
        Dim Btn As Button = TryCast(sender, Button)
        If Btn Is Nothing Then Stop
        Dim Fct As FCPFunction = TryCast(Btn.Tag, FCPFunction)
        Fct.ValFromControl()
        Fct.SetDitry()
    End Sub


End Class

Class FCPFunction
    Private _FctID As Integer
    Private _Name As String
    Private _Properties As New FCPPropertyList
    Private _Ditry As Boolean = False
    Public Sub FCP_Set(SP As SerialPort)
        Me._Ditry = False
        Dim SB As New StringBuilder()
        Dim NC As Integer = Me.NibbleCount
        SB.Append(Chr(&H50 + NC + 1))
        SB.Append(String.Format("{0,1:X1}", Me._FctID))
        SB.Append("1") ' Set / SetGet
        Me.EnQueueData(SB)
        If SP.IsOpen Then
            SP.Write(SB.ToString)
        Else
            Debug.Print(SB.ToString & " (COM closed)")
        End If
    End Sub
    Public Sub ValFromControl()
        For Each p As FCPProperty In Me.Properties
            p.valfromcontrol()
        Next
    End Sub
    Public Sub SetDitry()
        Me._Ditry = True
        frmMain.Timer.Enabled = True
    End Sub
    Public Function IsDirty() As Boolean
        Return Me._Ditry
    End Function
    Private Sub New()
    End Sub
    Public Function FctID() As Integer
        Return Me._FctID
    End Function
    Public Function Name() As String
        Return Me._Name
    End Function
    Shared Function Create(XFct As XElement) As FCPFunction
        Dim NewFCPFunction As New FCPFunction
        If Not XFct.@id.StartsWith("0x") Then Stop
        NewFCPFunction._FctID = Convert.ToInt32(XFct.@id.Substring(2), 16)
        NewFCPFunction._Name = XFct.<Text>.Value
        For Each XProp In XFct.<Data>.<Prop>
            Dim NP As FCPProperty
            NP = FCPProperty.Create(XProp, NewFCPFunction)
            NewFCPFunction._Properties.Add(NP)
        Next
        Return NewFCPFunction
    End Function
    Public Function NibbleCount() As Integer
        Return Me._Properties.NibbleCount
    End Function
    Public Function DeQueueData(Q As Queue(Of Integer)) As Boolean
        Dim AllValid As Boolean = True
        For Each P As FCPProperty In Me._Properties
            AllValid = (AllValid And P.DeQueueData(Q))
        Next
        If AllValid Then
            For Each P As FCPProperty In Me._Properties
                P.UpdateData()
            Next
        End If
        Return AllValid
    End Function
    Public Sub EnQueueData(SB As StringBuilder)
        For Each P As FCPProperty In Me._Properties
            P.EnQueueData(SB)
        Next
    End Sub
    Function Properties() As FCPPropertyList
        Return Me._Properties
    End Function
End Class

Class FCPProperty
    Private _Name As String
    Private _Type As String
    Private _NibbleCount As Integer
    Private TempVal As Object
    Private _Value As Object
    Public ControlSet As Control
    Public ControlGet As Control
    Public XML As XElement
    Public minD As Integer = 0
    Public maxD As Integer = 255
    Public [Function] As FCPFunction
    Public valid As Boolean = False
    Public Sub ValFromControl()
        Select Case Me.ControlSet.GetType
            Case GetType(TextBox)
                Dim i As Integer
                If Integer.TryParse(Me.ControlSet.Text, i) Then
                    Me.UISetValue(i)
                Else
                    Me.UISetValue(0)
                    Me.ControlSet.Text = Me.Value
                End If
            Case GetType(TrackBar)
                Me.UISetValue(DirectCast(Me.ControlSet, TrackBar).Value)
            Case Else : Stop
        End Select
    End Sub
    Public Sub UISetValue(value As Integer)
        If value < Me.minD Then value = Me.minD
        If value > Me.maxD Then value = Me.maxD
        Me._Value = value
        Me.Function.SetDitry()
        Me.valid = True
    End Sub

    Public Function NibbleCount() As String
        Return Me._NibbleCount
    End Function
    Public Function Name() As String
        Return Me._Name
    End Function
    Private Sub New()
    End Sub
    ''' <summary>
    ''' Erzeugt eine FCPProperty-Instanz aus einem XML-Element
    ''' </summary><param name="XProp">
    ''' </param><returns>
    ''' </returns><remarks>
    ''' </remarks>
    Shared Function Create(XProp As XElement, F As FCPFunction) As FCPProperty
        Dim NewFCPProperty As New FCPProperty
        If Not XProp.Name.LocalName.Equals("Prop") Then Throw New ArgumentException("Element name 'Prop' expected", "XProp")
        'ToDo: Wie geht das mit Namespace?
        NewFCPProperty._Name = XProp.@name
        NewFCPProperty._Type = XProp.@type
        NewFCPProperty.XML = XProp
        NewFCPProperty.Function = F
        Select Case XProp.@type
            Case "uint16"
                NewFCPProperty._NibbleCount = 4
                NewFCPProperty.maxD = &HFFFF
            Case "uint8"
                NewFCPProperty._NibbleCount = 2
                NewFCPProperty.maxD = &HFF
            Case "uint4"
                NewFCPProperty._NibbleCount = 1
                NewFCPProperty.maxD = &HFF
            Case Else : Stop
        End Select
        If XProp.@minD <> "" Then NewFCPProperty.minD = XProp.@minD
        If XProp.@maxD <> "" Then NewFCPProperty.maxD = XProp.@maxD
        Return NewFCPProperty
    End Function
    Public Sub EnQueueData(SB As StringBuilder)
        Dim i As Integer
        i = Me.Value
        Select Case Me._Type
            Case "uint8"
                SB.Append(String.Format("{0,2:X2}", i))
            Case "uint4"
                SB.Append(String.Format("{0,1:X1}", i))
            Case Else : Stop
        End Select
    End Sub
    ''' <summary>
    ''' Daten aus Puffer holen und prüfen
    ''' </summary><param name="Q">
    ''' </param><returns>
    ''' True, falls Daten gültig
    ''' </returns><remarks></remarks>
    Public Function DeQueueData(Q As Queue(Of Integer)) As Boolean
        Dim LongVal As Integer
        Select Case Me._Type
            Case "uint16"
                If Integer.TryParse(ChrW(Q.Dequeue()), NumberStyles.HexNumber, Nothing, LongVal) Then
                    Me.TempVal = LongVal << 12
                    If Integer.TryParse(ChrW(Q.Dequeue()), NumberStyles.HexNumber, Nothing, LongVal) Then
                        Me.TempVal += LongVal << 8
                        If Integer.TryParse(ChrW(Q.Dequeue()), NumberStyles.HexNumber, Nothing, LongVal) Then
                            Me.TempVal += LongVal << 4
                            If Integer.TryParse(ChrW(Q.Dequeue()), NumberStyles.HexNumber, Nothing, LongVal) Then
                                Me.TempVal += LongVal
                                'Todo: Wertprüfung (min/max usw.)
                                Return True
                            End If
                        End If
                    End If
                End If
            Case "uint8"
                If Integer.TryParse(ChrW(Q.Dequeue()), NumberStyles.HexNumber, Nothing, LongVal) Then
                    Me.TempVal = LongVal << 4
                    If Integer.TryParse(ChrW(Q.Dequeue()), NumberStyles.HexNumber, Nothing, LongVal) Then
                        Me.TempVal += LongVal
                        'Todo: Wertprüfung (min/max usw.)
                        Return True
                    End If
                End If
            Case "uint4"
                If Integer.TryParse(ChrW(Q.Dequeue()), NumberStyles.HexNumber, Nothing, LongVal) Then
                    Me.TempVal = LongVal
                    'Todo: Wertprüfung (min/max usw.)
                    Return True
                End If
            Case Else : Stop
        End Select
        Debug.Print("Invalid Data: " & Me.Name & " = " & Me.TempVal)
        Return False 'dummy
    End Function
    Public Sub UpdateData()
        Me._Value = Me.TempVal
        ControlSet.BeginInvoke(New frmMain.DeclareCheapGuiAccess(AddressOf frmMain.DoCheapGuiAccess), Me.ControlGet, Me.TempVal.ToString)
        If Not Me.valid Then
            Me.valid = True
            ControlSet.BeginInvoke(New frmMain.DeclareCheapGuiAccess(AddressOf frmMain.DoCheapGuiAccess), Me.ControlSet, Me.TempVal.ToString)
        End If
    End Sub
    Public Function Value() As Object
        Return Me._Value
    End Function
End Class

Class FCPPropertyList
    Inherits KeyedCollection(Of String, FCPProperty)
    Private _NibbleCount As Integer = Integer.MinValue
    Protected Overrides Function GetKeyForItem(item As FCPProperty) As String
        Return item.Name
    End Function
    Protected Overrides Sub ClearItems()
        MyBase.ClearItems()
        _NibbleCount = 0
    End Sub
    Protected Overrides Sub InsertItem(index As Integer, item As FCPProperty)
        MyBase.InsertItem(index, item)
        _NibbleCount = Integer.MinValue
    End Sub
    Protected Overrides Sub RemoveItem(index As Integer)
        MyBase.RemoveItem(index)
        _NibbleCount = Integer.MinValue
    End Sub
    Protected Overrides Sub SetItem(index As Integer, item As FCPProperty)
        MyBase.SetItem(index, item)
        _NibbleCount = Integer.MinValue
    End Sub
    Public Function NibbleCount() As Integer
        Dim NC As Integer = 0
        If Me._NibbleCount < 0 Then
            For Each P As FCPProperty In Items
                NC += P.NibbleCount
            Next
            Me._NibbleCount = NC
            Return NC
        Else
            Return Me._NibbleCount
        End If
    End Function
End Class

Class FCPFunctionList
    Inherits KeyedCollection(Of Integer, FCPFunction)
    Protected Overrides Function GetKeyForItem(item As FCPFunction) As Integer
        Return item.FctID
    End Function
End Class
