Imports System.IO
Imports System.Runtime.InteropServices
Imports DevExpress.XtraBars
Imports Microsoft.VisualBasic.FileIO
Imports NTSInformatica.CLN__STD

Public Class FROCRGSOF
    Inherits FRMCRGSOF

#Region "Form e toolbar"
    Public WithEvents tlb_hhImporta As NTSInformatica.NTSBarButtonItem
    Private Property strTipork As String = "!"
    Private Property strSerie As String = "-"
    Private Property lAnno As Integer = Now.Year
    Private Property lCodLead As Integer = 2049

    Public _oClfGsof As CLFCRGSOF
    Public Property oClfGsof() As CLFCRGSOF
        Get
            If _oClfGsof Is Nothing Then _oClfGsof = CType(oCleGsof, CLFCRGSOF)
            Return _oClfGsof
        End Get
        Set(ByVal value As CLFCRGSOF)
            _oClfGsof = value
        End Set
    End Property
    Public Overrides Sub InitializeComponent()
        MyBase.InitializeComponent()
        Me.tlb_hhImporta = New NTSInformatica.NTSBarButtonItem()
        Me.NtsBarManager1.Items.Add(Me.tlb_hhImporta)
        Me.tlbMain.LinksPersistInfo.Add(New DevExpress.XtraBars.LinkPersistInfo(Me.tlb_hhImporta))
        Me.tlb_hhImporta.Glyph = GetGlyphImporta()
        Me.tlb_hhImporta.Caption = "Importa offerta"
    End Sub
    Public Overridable Sub tlb_hhImporta_ItemClick(ByVal sender As System.Object, ByVal e As DevExpress.XtraBars.ItemClickEventArgs) Handles tlb_hhImporta.ItemClick
        Try
            ' Importa le righe dell'offerta selezionata.
            Dim fileOff As String = SelezionaFileOfferta()
            If String.IsNullOrWhiteSpace(fileOff) Then Return

            Dim dttOffCSV As DataTable = CsvToDataSet(fileOff, ";", True)
            If dttOffCSV Is Nothing OrElse dttOffCSV.Rows.Count = 0 Then
                oApp.MsgBoxErr("Il file selezionato non contiene righe da importare.")
                Return
            End If
            If dttOffCSV.Columns.Count < 13 Then
                oApp.MsgBoxErr("Il file selezionato non contiene tutte le colonne richieste.")
                Return
            End If

            Dim primaRiga As DataRow = dttOffCSV.Rows(0)

            Dim td_riferim As String = NTSCStr(primaRiga.ItemArray(0))
            If String.IsNullOrWhiteSpace(td_riferim) Then
                oApp.MsgBoxErr("Il riferimento dell'offerta non è valorizzato.")
                Return
            End If

            For Each dtrOfferta As DataRow In dttOffCSV.Rows
                Dim strCodiceRiga As String = NTSCStr(dtrOfferta.ItemArray(0)).Trim()
                If Not String.Equals(strCodiceRiga, td_riferim.Trim(),
                                     StringComparison.OrdinalIgnoreCase) Then
                    oApp.MsgBoxErr("Il file offerta contiene codici commessa differenti: '" &
                                   td_riferim.Trim() & "' e '" & strCodiceRiga & "'.")
                    Return
                End If
            Next

            If Not NuovaOfferta(td_riferim) Then
                oApp.MsgBoxErr("Errore durante la creazione della nuova offerta.")
                Return
            End If

            For Each tmpRiga As DataRow In dttOffCSV.Rows

                Dim mo_hhnumvoce As String = NTSCStr(tmpRiga.ItemArray(2)).Trim()
                Dim mo_descr As String = NTSCStr(tmpRiga.ItemArray(4))
                Dim mo_quant As Decimal = NTSCDec(tmpRiga.ItemArray(5))
                Dim mo_hhlarg As Decimal = NTSCDec(tmpRiga.ItemArray(6))
                Dim mo_hhlung As Decimal = NTSCDec(tmpRiga.ItemArray(7))
                Dim mo_hhcolore As String = NTSCStr(tmpRiga.ItemArray(12))

                If Not AggiungiRigaOfferta(mo_hhnumvoce, mo_descr, mo_quant, mo_hhlarg, mo_hhlung, mo_hhcolore) Then
                    oApp.MsgBoxErr("Errore durante l'inserimento della riga: " & mo_descr)
                    Return
                End If

            Next

            Dim numdoc As Integer = NTSCInt(oCleGsof.dsShared.Tables("TESTA").Rows(0)!et_numdoc)

            oCleGsof.bNew = True

            If Not SalvaOfferta() Then
                oApp.MsgBoxErr("Errore durante il salvataggio dell'offerta.")
                Return
            End If

            Me.edNumDoc.TextInt = numdoc
            tlbApri_ItemClick(Me, Nothing)

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Sub
    Public Overrides Sub edNumDoc_Validated(sender As Object, e As EventArgs)
        Try

            MyBase.edNumDoc_Validated(sender, e)
            If edNumDoc.TextInt = 0 Then
                Me.tlb_hhImporta.Visible = True
            Else
                Me.tlb_hhImporta.Visible = False
            End If

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Sub
    Public Overrides Sub tlbSalva_ItemClick(sender As Object, e As ItemClickEventArgs)
        Try

            MyBase.tlbSalva_ItemClick(sender, e)

            If ckEt_confermato.Checked Then

                If oApp.MsgBoxInfoYesNo_DefNo(
                    "Desideri generare l'impegno cliente R e l'ordine di produzione H?") = DialogResult.Yes Then
                    GeneraImpegniAssociati()
                End If

            End If

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Sub
#End Region

#Region "Gestione offerte"
    Private Function InizializzaBECRGSOF(Optional ByVal remoteHandler As NTSEventHandler = Nothing) As Boolean

        Try

            If oCleGsof IsNot Nothing Then Return True

            If Not oCleGsof Is Nothing Then Return True

            Dim strErr As String = ""
            Dim oTmp As Object = Nothing

            If CLN__STD.NTSIstanziaDll(oApp.ServerDir, oApp.NetDir, "BNCRGSOF", "BECRGSOF", oTmp, strErr, False, "", "") = False Then
                Throw New NTSException(oApp.Tr(Me, 128607611686875000,
                "ERRORE in fase di creazione Entity:" & vbCrLf & "|" & strErr & "|"))
                Return False
            End If

            oCleGsof = CType(oTmp, CLECRGSOF)

            AddHandler oCleGsof.RemoteEvent, AddressOf GestisciEventiEntity

            If oCleGsof.Init(oApp, oScript, CType(oApp.oMenu, CLE__MENU).oCleComm, "", False, "", "") = False Then Return False
            If Not oCleGsof.InitExt() Then Return False

            oCleGsof.bModuloCRM = False
            oCleGsof.bIsCRMUser = False

            Return True

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Function
    Private Sub GestisciEventiEntity_Pers(ByVal sender As Object, ByRef e As NTSEventArgs)

        Try

            If e.Message = "" Then Return

            If e.Message.Contains("Qualcosa") Then
                Return 'LO IGNORO O QUALSIASI ALTRA COSA.
            End If

        Catch ex As Exception
            MsgBox(ex.ToString)
        End Try

    End Sub
    Private Function NuovaOfferta(strRiferim As String) As Boolean

        Dim lNumdoc As Integer = 0

        Try

            Dim ds As DataSet = oCleGsof.dsShared

            If Not InizializzaBECRGSOF() Then Return False

            If lNumdoc = 0 Then
                lNumdoc = oCleGsof.LegNuma(strTipork, strSerie, lAnno)
            End If

            If lNumdoc = 0 Then Return False

            If Not oCleGsof.ApriOfferta(oApp.Ditta, True, strTipork, lAnno, strSerie, lNumdoc, 0, ds) Then
                Return False
            End If

            If Not oCleGsof.NuovoOfferta(oApp.Ditta, strTipork, lAnno, strSerie, lNumdoc, 0, "") Then
                Return False
            End If

            oCleGsof.ResetVar()

            oCleGsof.strVisNoteConto = "N"

            If oCleGsof.dttET Is Nothing OrElse oCleGsof.dttET.Rows.Count = 0 Then Return False

            With oCleGsof.dttET.Rows(0)

                !et_codlead = lCodLead
                !et_vers = 1
                !et_riferim = strRiferim
                !et_datdoc = Now.Date
                !et_datcreaz = Now.Date
                !et_orcreaz = Decimal.Parse(Now.ToString("HH.mm"), Globalization.CultureInfo.InvariantCulture)
                !et_opcreaz = oApp.User.Nome
                !et_datcons = DateSerial(Now.Year, 12, 31)
                !et_tipobf = 1
                !et_ultagg = Now.Date
                !et_datpromo = Now.Date
                !et_orpromo = Decimal.Parse(Now.ToString("HH.mm"), Globalization.CultureInfo.InvariantCulture)

            End With

            Return True

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Function
    Private Function AggiungiRigaOfferta(strNumeroVoce As String, strDescr As String, lQta As Decimal,
                                         nLarg As Decimal, nLung As Decimal, strColore As String) As Boolean

        Try

            If oCleGsof Is Nothing OrElse oCleGsof.dsShared.Tables("CORPO") Is Nothing Then Return False

            oCleGsof.dsShared.Tables("CORPO").Rows.Add()

            With oCleGsof.dsShared.Tables("CORPO").Rows(oCleGsof.dsShared.Tables("CORPO").Rows.Count - 1)

                !ec_vers = 1
                !ec_riga = oCleGsof.dsShared.Tables("CORPO").Rows.Count
                !ec_codart = "D"
                !ec_hhnumvoce = strNumeroVoce
                !ec_descr = strDescr
                !ec_quant = lQta
                !ec_hhlarg = nLarg
                !ec_hhlung = nLung
                !ec_hhcolore = strColore
                !ec_datcons = DateSerial(Now.Year, 12, 31)
                !ec_unmis = "ND"
                !ec_ultagg = Now.Date

            End With

            Return True

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Function
    Private Function SalvaOfferta() As Boolean
        Try

            Return oCleGsof.SalvaOfferta("N")

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Function
#End Region

#Region "Selettori e lettura CSV"
    Private Function GetGlyphImporta() As Image
        GetGlyphImporta = Nothing
        Try

            Dim b64 As String = "iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/
                             9hAAAAfklEQVR4nGNgoBAwwlkNDP9RZH5gUd2BpB4K
                             WHCZ/L8d1TzGSkaGgBMISzZYQAxjItapuAB1Dfhf/5
                             /hf/1/XGpp4wKUQGRsxAhkggBnNCJ7JfAkboOJ8sJ6
                             c+zhssGCgZF+0bhhA6btJBlAsQuQbYXRDAx48gJjJe
                             lRShYAAKTDGxk6mxg2AAAAAElFTkSuQmCC"

            Dim bytes() As Byte = Convert.FromBase64String(b64)

            Using ms As New MemoryStream(bytes)
                GetGlyphImporta = Image.FromStream(ms)
            End Using

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Function
    Private Function SelezionaFileOfferta() As String
        SelezionaFileOfferta = ""
        Try

            Using ofd As New OpenFileDialog

                ofd.Title = "Seleziona file offerta"

                ofd.Filter = "File CSV (*.csv)|*.csv|Tutti i file (*.*)|*.*"

                ofd.FilterIndex = 1
                ofd.RestoreDirectory = True
                ofd.Multiselect = False

                If ofd.ShowDialog() = DialogResult.OK Then
                    SelezionaFileOfferta = ofd.FileName
                Else
                    SelezionaFileOfferta = String.Empty
                End If

            End Using

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try
    End Function
    Private Function SelezionaCartellaCommessa() As String
        SelezionaCartellaCommessa = ""
        Try

            SelezionaCartellaCommessa = FolderPicker.Seleziona(Me.Handle,
                "Seleziona la cartella contenente i file della commessa")

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function
    Public Function CsvToDataSet(filePath As String,
                             Optional delimiter As String = ";",
                             Optional hasHeader As Boolean = False) As DataTable

        CsvToDataSet = New DataTable("CsvData")

        Try

            If String.IsNullOrWhiteSpace(filePath) OrElse Not File.Exists(filePath) Then Return CsvToDataSet

            Using parser As New TextFieldParser(filePath)

                parser.TextFieldType = FieldType.Delimited
                parser.SetDelimiters(delimiter)
                parser.HasFieldsEnclosedInQuotes = True

                If Not parser.EndOfData Then

                    Dim firstRow() As String = parser.ReadFields()

                    If hasHeader AndAlso CheckIntestazione(firstRow) Then
                        'L'import usa gli indici di colonna: assegno nomi tecnici
                        'univoci senza dipendere da intestazioni vuote o duplicate.
                        For i As Integer = 0 To firstRow.Length - 1
                            CsvToDataSet.Columns.Add("Column" & (i + 1).ToString())
                        Next
                    Else
                        For i As Integer = 0 To firstRow.Length - 1
                            CsvToDataSet.Columns.Add("Column" & (i + 1).ToString())
                        Next

                        CsvToDataSet.Rows.Add(firstRow)
                    End If
                End If

                While Not parser.EndOfData
                    Dim fields() As String = parser.ReadFields()

                    While fields.Length > CsvToDataSet.Columns.Count
                        CsvToDataSet.Columns.Add("Column" & (CsvToDataSet.Columns.Count + 1).ToString())
                    End While

                    CsvToDataSet.Rows.Add(fields)
                End While

            End Using

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        End Try

    End Function

    Private Function CheckIntestazione(ByVal valori As String()) As Boolean
        Try
            If valori Is Nothing OrElse valori.Length = 0 Then Return False

            Dim strPrimoValore As String = NTSCStr(valori(0)).Trim()
            If String.Equals(strPrimoValore, "TOL", StringComparison.OrdinalIgnoreCase) Then Return True

            Dim paroleIntestazione As String() = {
                "NOMECOMMESSA", "NUMEROVOCE", "CATEGORIA", "CODICE",
                "DESCRIZIONE", "TRATTAMENTO", "INTERNO", "ESTERNO",
                "LBARRA", "QTA", "QUANTITA", "CAMPOINSERIMENTO",
                "DATAORAESPORTAZIONE", "ETICHETTA", "CLASSE"
            }
            Dim nCorrispondenze As Integer = 0
            For Each valore As String In valori
                Dim strValore As String = NormalizzaCampoIntestazione(valore)
                If Array.IndexOf(paroleIntestazione, strValore) >= 0 Then nCorrispondenze += 1
            Next

            Return nCorrispondenze >= 2

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Private Function NormalizzaCampoIntestazione(ByVal valore As String) As String
        NormalizzaCampoIntestazione = String.Empty
        Try

            NormalizzaCampoIntestazione = NTSCStr(valore).Trim().ToUpperInvariant().
            Replace(" ", "").Replace(vbTab, "").Replace("/", "").
            Replace("-", "").Replace("_", "")

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

#End Region

#Region "Generazione filiera"
    Public Overridable Sub GeneraImpegniAssociati()
        Dim frmProgress As BOCRGSOF_ProgressView = Nothing
        Try
            ' Seleziona la commessa e avvia la generazione guidata.
            Dim strFolderPath As String = SelezionaCartellaCommessa()
            If String.IsNullOrWhiteSpace(strFolderPath) Then Return
            If oCleGsof.dttET Is Nothing OrElse oCleGsof.dttET.Rows.Count = 0 Then Return

            Dim dtrTestata As DataRow = oCleGsof.dttET.Rows(0)
            Dim strRiferimento As String = NTSCStr(dtrTestata!et_riferim).Trim
            Dim arrFileTXJ As String() = Directory.GetFiles(strFolderPath, "*_TXJ*.csv")
            If arrFileTXJ.Length = 0 Then
                oApp.MsgBoxErr("La cartella selezionata non contiene un file TXJ.")
                Return
            End If
            If arrFileTXJ.Length > 1 Then
                oApp.MsgBoxErr("La cartella selezionata contiene più file TXJ. Selezionare una cartella contenente una sola commessa.")
                Return
            End If

            Dim strNomeFile As String = Path.GetFileName(arrFileTXJ(0))
            Dim nPosTXJ As Integer = strNomeFile.IndexOf("_TXJ", StringComparison.OrdinalIgnoreCase)
            If nPosTXJ <= 0 Then
                oApp.MsgBoxErr("Il nome del file TXJ non rispetta il formato <commessa>_TXJ.CSV.")
                Return
            End If

            Dim strCodicePacchetto As String = strNomeFile.Substring(0, nPosTXJ).Trim()
            If Not String.Equals(strCodicePacchetto, strRiferimento,
                                 StringComparison.OrdinalIgnoreCase) Then
                oApp.MsgBoxErr("Il codice commessa del file TXJ non coincide con quello dell'offerta." &
                               vbCrLf & vbCrLf &
                               "Offerta: " & strRiferimento & vbCrLf &
                               "File TXJ: " & strCodicePacchetto)
                Return
            End If

            Dim strMsg As String = ""
            If Not ValidaCodiceCommessaTXJ(arrFileTXJ(0), strRiferimento, strMsg) Then
                oApp.MsgBoxErr(strMsg)
                Return
            End If

            frmProgress = New BOCRGSOF_ProgressView()
            If Not frmProgress.Init(CType(oApp.oMenu, CLE__MENU), Nothing, oApp.Ditta) Then Return
            frmProgress.InitEntity(oClfGsof)
            frmProgress.Show(Me)
            frmProgress.Aggiorna("Lettura dei file della commessa...", 0)

            If oClfGsof.GeneraFiliera(strFolderPath, strCodicePacchetto, strRiferimento,
                                      NTSCInt(dtrTestata!et_codlead), strMsg) Then
                oApp.MsgBoxInfo(strMsg)
            Else
                oApp.MsgBoxErr(strMsg)
            End If

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------	
        Finally
            If frmProgress IsNot Nothing Then frmProgress.Close()
        End Try

    End Sub

    Private Function ValidaCodiceCommessaTXJ(ByVal strFileTXJ As String,
                                             ByVal strCodiceAtteso As String,
                                             ByRef strMsg As String) As Boolean
        ValidaCodiceCommessaTXJ = False
        Try
            Using parser As New TextFieldParser(strFileTXJ)
                parser.TextFieldType = FieldType.Delimited
                parser.SetDelimiters(";")
                parser.HasFieldsEnclosedInQuotes = True

                Dim bRigaDatiTrovata As Boolean = False
                Dim bPrimaRigaSignificativa As Boolean = True
                While Not parser.EndOfData
                    Dim valori As String() = parser.ReadFields()
                    If valori Is Nothing OrElse valori.Length = 0 Then Continue While
                    If bPrimaRigaSignificativa Then
                        bPrimaRigaSignificativa = False
                        If CheckIntestazione(valori) Then Continue While
                    End If

                    Dim strCodiceRiga As String = NTSCStr(valori(0)).Trim()
                    If String.IsNullOrWhiteSpace(strCodiceRiga) Then
                        strMsg = "Il file TXJ contiene una riga con codice commessa vuoto."
                        Return False
                    End If
                    If Not String.Equals(strCodiceRiga, strCodiceAtteso,
                                         StringComparison.OrdinalIgnoreCase) Then
                        strMsg = "Il contenuto del TXJ non appartiene all'offerta corrente." &
                                 vbCrLf & vbCrLf &
                                 "Offerta: " & strCodiceAtteso & vbCrLf &
                                 "Codice nel TXJ: " & strCodiceRiga
                        Return False
                    End If
                    bRigaDatiTrovata = True
                End While

                If Not bRigaDatiTrovata Then
                    strMsg = "Il file TXJ non contiene righe dati."
                    Return False
                End If
            End Using

            Return True

        Catch ex As Exception
            strMsg = "Impossibile verificare il codice commessa del TXJ: " & ex.Message
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function
#End Region
End Class

#Region "Selettore cartella Windows"
Friend Class FolderPicker
    Private Const FOS_PICKFOLDERS As UInteger = &H20UI
    Private Const FOS_FORCEFILESYSTEM As UInteger = &H40UI
    Private Const FOS_PATHMUSTEXIST As UInteger = &H800UI
    Private Const SIGDN_FILESYSPATH As UInteger = &H80058000UI
    Private Const HRESULT_CANCELLED As Integer = -2147023673

    Public Shared Function Seleziona(ByVal hWndOwner As IntPtr, ByVal strTitolo As String) As String
        Seleziona = ""
        Dim oDialog As IFileDialog = Nothing
        Dim oItem As IShellItem = Nothing
        Dim ptrPath As IntPtr = IntPtr.Zero

        Try
            Dim tDialog As Type = Type.GetTypeFromCLSID(New Guid("DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7"))
            oDialog = CType(Activator.CreateInstance(tDialog), IFileDialog)

            Dim nOptions As UInteger = 0
            oDialog.GetOptions(nOptions)
            oDialog.SetOptions(nOptions Or FOS_PICKFOLDERS Or FOS_FORCEFILESYSTEM Or FOS_PATHMUSTEXIST)
            oDialog.SetTitle(strTitolo)

            Dim nResult As Integer = oDialog.Show(hWndOwner)
            If nResult = HRESULT_CANCELLED Then Return ""
            If nResult <> 0 Then Marshal.ThrowExceptionForHR(nResult)

            oDialog.GetResult(oItem)
            oItem.GetDisplayName(SIGDN_FILESYSPATH, ptrPath)
            If ptrPath <> IntPtr.Zero Then Seleziona = Marshal.PtrToStringUni(ptrPath)

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Nothing, "")
            '-------------------------------------------------
        Finally
            If ptrPath <> IntPtr.Zero Then Marshal.FreeCoTaskMem(ptrPath)
            If oItem IsNot Nothing Then Marshal.FinalReleaseComObject(oItem)
            If oDialog IsNot Nothing Then Marshal.FinalReleaseComObject(oDialog)
        End Try

    End Function

    <ComImport, Guid("42F85136-DB7E-439C-85F1-E4075D135FC8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface IFileDialog
        <PreserveSig>
        Function Show(ByVal parent As IntPtr) As Integer
        Sub SetFileTypes(ByVal cFileTypes As UInteger, ByVal rgFilterSpec As IntPtr)
        Sub SetFileTypeIndex(ByVal iFileType As UInteger)
        Sub GetFileTypeIndex(ByRef piFileType As UInteger)
        Sub Advise(ByVal pfde As IntPtr, ByRef pdwCookie As UInteger)
        Sub Unadvise(ByVal dwCookie As UInteger)
        Sub SetOptions(ByVal fos As UInteger)
        Sub GetOptions(ByRef pfos As UInteger)
        Sub SetDefaultFolder(ByVal psi As IShellItem)
        Sub SetFolder(ByVal psi As IShellItem)
        Sub GetFolder(ByRef ppsi As IShellItem)
        Sub GetCurrentSelection(ByRef ppsi As IShellItem)
        Sub SetFileName(<MarshalAs(UnmanagedType.LPWStr)> ByVal pszName As String)
        Sub GetFileName(<MarshalAs(UnmanagedType.LPWStr)> ByRef pszName As String)
        Sub SetTitle(<MarshalAs(UnmanagedType.LPWStr)> ByVal pszTitle As String)
        Sub SetOkButtonLabel(<MarshalAs(UnmanagedType.LPWStr)> ByVal pszText As String)
        Sub SetFileNameLabel(<MarshalAs(UnmanagedType.LPWStr)> ByVal pszLabel As String)
        Sub GetResult(ByRef ppsi As IShellItem)
        Sub AddPlace(ByVal psi As IShellItem, ByVal fdap As UInteger)
        Sub SetDefaultExtension(<MarshalAs(UnmanagedType.LPWStr)> ByVal pszDefaultExtension As String)
        Sub Close(ByVal hr As Integer)
        Sub SetClientGuid(ByRef guid As Guid)
        Sub ClearClientData()
        Sub SetFilter(ByVal pFilter As IntPtr)
    End Interface

    <ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>
    Private Interface IShellItem
        Sub BindToHandler(ByVal pbc As IntPtr, ByRef bhid As Guid, ByRef riid As Guid, ByRef ppv As IntPtr)
        Sub GetParent(ByRef ppsi As IShellItem)
        Sub GetDisplayName(ByVal sigdnName As UInteger, ByRef ppszName As IntPtr)
        Sub GetAttributes(ByVal sfgaoMask As UInteger, ByRef psfgaoAttribs As UInteger)
        Sub Compare(ByVal psi As IShellItem, ByVal hint As UInteger, ByRef piOrder As Integer)
    End Interface
End Class
#End Region
