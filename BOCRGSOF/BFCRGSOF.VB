Imports System.Data
Imports NTSInformatica.CLN__STD
Imports System.Globalization


Imports System
Partial Public Class CLFCRGSOF
  Inherits CLECRGSOF

#Region "Configurazione entity"
  Private strUltimoEventoFiliera As String = ""
    Private Const ContoOrdineProduzione As Integer = 19019999
    Private Const GeneraImpegnoCommessa As Boolean = False
    Private Const GeneraOrdineProduzione As Boolean = True
    Public Event AvanzamentoFiliera(ByVal strMessaggio As String, ByVal nPercentuale As Integer)

    Public _oClhGsof As CLHCRGSOF
    Public Property oClhGsof() As CLHCRGSOF
        Get
            If _oClhGsof Is Nothing Then _oClhGsof = CType(oCldGsof, CLHCRGSOF)
            Return _oClhGsof
        End Get
        Set(ByVal value As CLHCRGSOF)
            _oClhGsof = value
        End Set
    End Property
#End Region

#Region "Orchestrazione filiera"
    Public Overridable Function GeneraFiliera(ByVal strFolderPath As String,
                                            ByVal strCodicePreferito As String,
                                            ByVal strDescrizioneCommessa As String,
                                            ByVal nCodLead As Integer,
                                            ByRef strMsg As String) As Boolean
        GeneraFiliera = False
        Dim dsImport As New DataSet

        Try
            ' Coordina import, distinte e impegni della commessa.
            strMsg = ""
            Dim strCodice As String = oClhGsof.RisolviCodicePacchetto(strFolderPath, strCodicePreferito)
            If String.IsNullOrWhiteSpace(strCodice) Then
                strMsg = "Pacchetto TXJ non trovato o non identificabile."
                Return False
            End If
            Dim nConto As Integer = oClhGsof.RisolviContoCliente(nCodLead, 9010001)

            dsImport = oClhGsof.CaricaPacchettoCSV(strFolderPath, strCodice)
            If dsImport Is Nothing Then
                strMsg = "File TXJ o TXO mancanti o privi di dati."
                Return False
            End If
            If Not ValidaCollegamentiOfferta(dsImport, strMsg) Then Return False
            MostraAvanzamento("Lettura file della commessa completata.", 100)

            Dim nCommessa As Integer = oClhGsof.CreaRestituisciCommessa(strDescrizioneCommessa)
            If nCommessa = 0 Then
                strMsg = "Impossibile creare o recuperare la commessa."
                Return False
            End If
            Dim bImpegnoCliente As Boolean = oClhGsof.EsisteImpegno(nCommessa, "R")
            Dim bImpegnoCommessa As Boolean =
                Not GeneraImpegnoCommessa OrElse oClhGsof.EsisteImpegno(nCommessa, "#")
            Dim bOrdineProduzione As Boolean =
                Not GeneraOrdineProduzione OrElse oClhGsof.EsisteImpegno(nCommessa, "H")

            'Gli articoli e le distinte vengono riallineati a ogni elaborazione.
            'Per una distinta esistente viene creata una nuova versione corrente.
            If Not CreaArticoliEDistinte(dsImport, nCommessa, strMsg) Then Return False

            If bImpegnoCliente AndAlso bImpegnoCommessa AndAlso bOrdineProduzione Then
                MostraAvanzamento("Gestione distinte base completata.", 100)
                strMsg = $"Gestite le distinte base della commessa {nCommessa}; l'impegno cliente e l'ordine di produzione erano già presenti."
                Return True
            End If

            Dim dttProdotti As DataTable = Nothing
            If Not bImpegnoCliente OrElse Not bOrdineProduzione Then
                dttProdotti = CreaRigheProdottiFiniti(dsImport)
            End If
            If (Not bImpegnoCliente OrElse Not bOrdineProduzione) AndAlso
               (dttProdotti Is Nothing OrElse dttProdotti.Rows.Count = 0) Then
                strMsg = "Il TXJ non contiene prodotti finiti validi."
                Return False
            End If

            Dim dttMateriali As DataTable = Nothing
            If GeneraImpegnoCommessa AndAlso Not bImpegnoCommessa Then
                dttMateriali = CreaFabbisogniMateriali(dsImport)
            End If
            If GeneraImpegnoCommessa AndAlso Not bImpegnoCommessa AndAlso
               (dttMateriali Is Nothing OrElse dttMateriali.Rows.Count = 0) Then
                strMsg = "Non sono stati ricavati materiali per l'impegno di commessa."
                Return False
            End If

            If Not bImpegnoCliente AndAlso
               Not CreaImpegno("R", nConto, nCommessa, strCodice, dttProdotti, strMsg) Then Return False

            If GeneraImpegnoCommessa AndAlso Not bImpegnoCommessa AndAlso
               Not CreaImpegno("#", nConto, nCommessa, strCodice, dttMateriali, strMsg) Then Return False

            If GeneraOrdineProduzione AndAlso Not bOrdineProduzione AndAlso
               Not CreaImpegno("H", 9019999, nCommessa, strCodice, dttProdotti, strMsg) Then Return False

            MostraAvanzamento("Elaborazione completata.", 100)
            strMsg = $"Completata la filiera documentale per la commessa {nCommessa}: impegno cliente R e ordine di produzione H."

            Return True

        Catch ex As Exception
            strMsg = ex.Message
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function
#End Region

#Region "Collegamento offerta - TXJ"
    Private Function RigheOffertaAttive() As List(Of DataRow)

        RigheOffertaAttive = New List(Of DataRow)

        Try

            If dsShared Is Nothing OrElse Not dsShared.Tables.Contains("CORPO") Then Return RigheOffertaAttive
            For Each dtr As DataRow In dsShared.Tables("CORPO").Rows
                If dtr.RowState <> DataRowState.Deleted Then RigheOffertaAttive.Add(dtr)
            Next

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Private Function ValidaCollegamentiOfferta(ByVal dsImport As DataSet,
                                               ByRef strMsg As String) As Boolean

        ValidaCollegamentiOfferta = False

        Try

            If dsShared Is Nothing OrElse Not dsShared.Tables.Contains("CORPO") Then
                strMsg = "Corpo dell'offerta non disponibile."
                Return False
            End If
            If Not dsShared.Tables("CORPO").Columns.Contains("ec_hhnumvoce") Then
                strMsg = "Campo ec_hhnumvoce non disponibile sul corpo dell'offerta."
                Return False
            End If
            If dsImport Is Nothing OrElse Not dsImport.Tables.Contains("JOB") OrElse
               Not dsImport.Tables("JOB").Columns.Contains("NumeroVoce") Then
                strMsg = "Tabella JOB del TXJ non disponibile o priva del campo NumeroVoce."
                Return False
            End If

            Dim voci As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)
            For Each dtr As DataRow In RigheOffertaAttive()
                'Le righe a quantità zero non generano impegni né fabbisogni:
                'possono quindi avere il collegamento nullable non valorizzato.
                If NTSCDec(dtr!ec_quant) = 0D Then Continue For

                Dim strVoce As String = NTSCStr(dtr!ec_hhnumvoce).Trim()
                If String.IsNullOrWhiteSpace(strVoce) Then
                    strMsg = "La riga offerta " & NTSCStr(dtr!ec_riga) &
                             " non e' collegata a una NumeroVoce Tekla."
                    Return False
                End If
                If Not voci.Add(strVoce) Then
                    strMsg = "NumeroVoce Tekla duplicata nell'offerta: " & strVoce & "."
                    Return False
                End If
                Dim righeJob As DataRow() = TrovaRigheJob(dsImport, strVoce)
                If righeJob.Length = 0 Then
                    strMsg = "NumeroVoce " & strVoce & " non trovata nel file TXJ."
                    Return False
                End If
                If righeJob.Length > 1 Then
                    strMsg = "NumeroVoce " & strVoce & " duplicata nel file TXJ."
                    Return False
                End If
            Next

            If voci.Count = 0 Then
                strMsg = "L'offerta non contiene righe collegate al file TXJ."
                Return False
            End If

            Return True

        Catch ex As Exception
            strMsg = ex.Message
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Private Function TrovaRigheJob(ByVal dsImport As DataSet,
                                   ByVal strNumeroVoce As String) As DataRow()

        Try

            If dsImport Is Nothing OrElse Not dsImport.Tables.Contains("JOB") Then
                Return New DataRow() {}
            End If
            Return dsImport.Tables("JOB").Select(
                "NumeroVoce = " & CStrSQL(NTSCStr(strNumeroVoce).Trim()))

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
            Return New DataRow() {}
        End Try

    End Function

    Private Function ValoreDecimaleRiga(ByVal dtr As DataRow, ByVal strCampo As String) As Decimal

        Try

            If dtr Is Nothing OrElse Not dtr.Table.Columns.Contains(strCampo) Then Return 0D
            Return NTSCDec(dtr(strCampo))

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Private Function ValoreStringaRiga(ByVal dtr As DataRow, ByVal strCampo As String) As String

        ValoreStringaRiga = String.Empty

        Try

            If dtr Is Nothing OrElse Not dtr.Table.Columns.Contains(strCampo) Then Return ""
            ValoreStringaRiga = NTSCStr(dtr(strCampo))

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function
#End Region

#Region "Generazione impegni"
    Private Function CreaRigheProdottiFiniti(ByVal dsImport As DataSet) As DataTable

        CreaRigheProdottiFiniti = New DataTable("PRODOTTI")

        Try

            CreaRigheProdottiFiniti.Columns.Add("codart", GetType(String))
            CreaRigheProdottiFiniti.Columns.Add("descr", GetType(String))
            CreaRigheProdottiFiniti.Columns.Add("quant", GetType(Decimal))
            CreaRigheProdottiFiniti.Columns.Add("larg", GetType(Decimal))
            CreaRigheProdottiFiniti.Columns.Add("lung", GetType(Decimal))
            CreaRigheProdottiFiniti.Columns.Add("colore", GetType(String))
            CreaRigheProdottiFiniti.Columns.Add("numvoce", GetType(String))

            For Each dtrOfferta As DataRow In RigheOffertaAttive()
                Dim dQuantita As Decimal = NTSCDec(dtrOfferta!ec_quant)
                If dQuantita = 0D Then Continue For

                Dim strNumeroVoce As String = NTSCStr(dtrOfferta!ec_hhnumvoce).Trim()
                Dim righeJob As DataRow() = TrovaRigheJob(dsImport, strNumeroVoce)
                If righeJob.Length = 0 Then Continue For

                Dim dtr As DataRow = righeJob(0)
                Dim strCodart As String = ""
                Dim strDescrTecnica As String = ""
                ComponiArticolo(dtr, "JOB", strCodart, strDescrTecnica)
                If Not String.IsNullOrWhiteSpace(strCodart) Then
                    CreaRigheProdottiFiniti.Rows.Add(
                        strCodart,
                        NTSCStr(dtrOfferta!ec_descr),
                        dQuantita,
                        ValoreDecimaleRiga(dtrOfferta, "ec_hhlarg"),
                        ValoreDecimaleRiga(dtrOfferta, "ec_hhlung"),
                        ValoreStringaRiga(dtrOfferta, "ec_hhcolore"),
                        strNumeroVoce)
                End If
            Next

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Function CreaImpegno(ByVal strTipork As String, ByVal nConto As Integer,
                               ByVal nCommessa As Integer, ByVal strRiferimento As String,
                               ByVal dttRighe As DataTable, ByRef strMsg As String) As Boolean

        CreaImpegno = False

        Try

            ' Crea testata e righe dell'impegno richiesto.
            Dim strTipoImpegno As String
            Select Case strTipork
                Case "R"
                    strTipoImpegno = "impegno cliente"
                Case "#"
                    strTipoImpegno = "impegno di commessa"
                Case "H"
                    strTipoImpegno = "ordine di produzione"
                Case Else
                    strMsg = "Tipo documento non previsto per la generazione: " & strTipork & "."
                    Return False
            End Select
            Dim nContoDocumento As Integer = If(strTipork = "H", ContoOrdineProduzione, nConto)
            MostraAvanzamento("Creazione " & strTipoImpegno & "...", 0)
            Dim oCleGsor As CLEORGSOR = Nothing
            Dim strErr As String = ""
            Dim oTmp As Object = Nothing

            If Not CLN__STD.NTSIstanziaDll(oApp.ServerDir, oApp.NetDir, "BNORGSOR", "BEORGSOR", oTmp, strErr, False, "", "") Then
                strMsg = strErr
                Return False
            End If

            oCleGsor = CType(oTmp, CLEORGSOR)
            AddHandler oCleGsor.RemoteEvent, AddressOf GestisciEventiFiliera
            If Not oCleGsor.Init(oApp, Nothing, CType(oApp.oMenu, CLE__MENU).oCleComm, "", False, "", "") Then Return False
            If Not oCleGsor.InitExt() Then Return False

            Dim strSerie As String = " "
            Dim nAnno As Integer = Now.Year
            Dim nNumero As Integer = oCleGsor.LegNuma(strTipork, strSerie, nAnno)
            If nNumero = 0 Then
                strMsg = $"Numerazione non configurata per il tipo documento {strTipork}."
                Return False
            End If

            Dim ds As New DataSet
            oCleGsor.bDisabilitaCheckAnnoData = True
            oCleGsor.bSaltaTestAnnoCompCertif = True
            oCleGsor.bDisabilitaCheckDateAnteriori = True
            If Not oCleGsor.ApriOrdine(oApp.Ditta, True, strTipork, nAnno, strSerie, nNumero, ds) Then Return False
            If ds.Tables.Contains("TESTA") AndAlso ds.Tables("TESTA").Rows.Count > 0 Then Return False

            oCleGsor.ResetVar()
            oCleGsor.strVisNoteConto = "N"
            If Not oCleGsor.NuovoOrdine(oApp.Ditta, strTipork, nAnno, strSerie, nNumero, "") Then Return False

            With oCleGsor.dttET.Rows(0)
                !et_conto = nContoDocumento
                !et_tipobf = If(strTipork = "H", 3, 1)
                !et_magaz = 1
                !et_commeca = nCommessa
                !et_riferim = strRiferimento
                !et_datdoc = Now.Date
            End With
            If Not oCleGsor.OkTestata Then
                strMsg = $"Testata non valida per il documento {strTipork}."
                Return False
            End If
            If strTipork = "H" Then
                'OkTestata può ricalcolare alcuni valori di testata: il conto
                'e il tipo bolla dell'ordine di produzione restano fissi.
                oCleGsor.dttET.Rows(0)!et_conto = ContoOrdineProduzione
                oCleGsor.dttET.Rows(0)!et_tipobf = 3
            End If

            Dim nRiga As Integer = 0
            For Each dtr As DataRow In dttRighe.Rows
                If Not oCleGsor.AggiungiRigaCorpo(False, NTSCStr(dtr!codart), 0, 0) Then Return False

                Dim nIndex As Integer = oCleGsor.dttEC.Rows.Count - 1
                With oCleGsor.dttEC.Rows(nIndex)
                    !ec_descr = NTSCStr(dtr!descr)
                    !ec_quant = NTSCDec(dtr!quant)
                    If .Table.Columns.Contains("ec_hhlarg") AndAlso
                       dtr.Table.Columns.Contains("larg") Then !ec_hhlarg = NTSCDec(dtr!larg)
                    If .Table.Columns.Contains("ec_hhlung") AndAlso
                       dtr.Table.Columns.Contains("lung") Then !ec_hhlung = NTSCDec(dtr!lung)
                    If .Table.Columns.Contains("ec_hhcolore") AndAlso
                       dtr.Table.Columns.Contains("colore") Then !ec_hhcolore = NTSCStr(dtr!colore)
                    If .Table.Columns.Contains("ec_hhnumvoce") Then
                        If dtr.Table.Columns.Contains("numvoce") Then
                            !ec_hhnumvoce = NTSCStr(dtr!numvoce).Trim()
                        Else
                            'Le righe aggregate dell'impegno # non hanno una
                            'singola NumeroVoce di origine.
                            !ec_hhnumvoce = DBNull.Value
                        End If
                    End If
                    If String.IsNullOrWhiteSpace(NTSCStr(!ec_unmis)) Then !ec_unmis = "PZ"
                End With
                If Not oCleGsor.RecordSalva(nIndex, False, Nothing) Then Return False
                nRiga += 1
                MostraAvanzamento("Creazione " & strTipoImpegno & ": riga " & nRiga.ToString() &
                                  " di " & dttRighe.Rows.Count.ToString(), Percentuale(nRiga, dttRighe.Rows.Count))
            Next

            If strTipork = "H" Then
                oCleGsor.dttET.Rows(0)!et_conto = ContoOrdineProduzione
                oCleGsor.dttET.Rows(0)!et_tipobf = 3
            End If
            If Not oCleGsor.SalvaOrdine("N") Then
                strMsg = $"Errore durante il salvataggio del documento {strTipork}."
                Return False
            End If

            Return True

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function
#End Region

#Region "Avanzamento"
    Private Sub GestisciEventiFiliera(ByVal sender As Object, ByRef e As NTSEventArgs)

        Try

            If e IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(e.Message) Then strUltimoEventoFiliera = e.Message

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub

    Private Sub MostraAvanzamento(ByVal strMessaggio As String, ByVal nPercentuale As Integer)

        Try

            RaiseEvent AvanzamentoFiliera(strMessaggio, nPercentuale)

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub

    Private Function Percentuale(ByVal nCorrente As Integer, ByVal nTotale As Integer) As Integer

        Percentuale = 0

        Try

            If nTotale > 0 Then Percentuale = Math.Min(100, CInt((nCorrente * 100D) / nTotale))

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function
#End Region

#Region "Importazione commessa"
    Private nCommessaImport As Integer
    Private strVersioneImport As String = "000"
    Private strUnMisArticolo As String = "PZ"
    Private nCodIvaArticolo As Integer = 1022
    Private nContropArticolo As Integer = 1
    Private nControaArticolo As Integer = 51
    Private strClasseProdottoFinito As String = ""

#Region "Inizializzazione import"
    Private Function CreaArticoliEDistinte(ByVal dsImport As DataSet, ByVal nCommessa As Integer,
                                           ByRef strMsg As String) As Boolean

        CreaArticoliEDistinte = False

        Try

            ' Genera prima gli articoli e poi le relative distinte.
            If Not oApp.oGvar.UsaVersioneDistinta(oApp.Ditta) Then
                strMsg = "Abilitare la gestione delle versioni delle distinte base."
                Return False
            End If

            strUnMisArticolo = oClhGsof.LeggiImpostazione("UnMis_Artico", "PZ")
            nCodIvaArticolo = NTSCInt(oClhGsof.LeggiImpostazione("CodIva_Artico", "1022"))
            nContropArticolo = NTSCInt(oClhGsof.LeggiImpostazione("Controp_Artico", "1"))
            nControaArticolo = NTSCInt(oClhGsof.LeggiImpostazione("Controa_Artico", "51"))
            strClasseProdottoFinito = oClhGsof.LeggiImpostazione("CodCla1_ProdFin", "")

            If Not ValidaProfiliTXO(dsImport, strMsg) Then Return False

            Dim oCleArti As CLEMGARTI = Nothing
            Dim oCleDiba As CLEDBDIBA = Nothing
            If Not InizializzaMGARTI(oCleArti, strMsg) Then Return False

            Dim nTotaleArticoli As Integer = 0
            For Each dtt As DataTable In dsImport.Tables
                If dtt.TableName <> "OPTION" Then nTotaleArticoli += dtt.Rows.Count
            Next
            Dim nArticolo As Integer = 0
            MostraAvanzamento("Gestione articoli...", 0)
            For Each dtt As DataTable In dsImport.Tables
                'Il TXO descrive tagli e consumi delle barre censite nel TXB:
                'non genera una seconda anagrafica articolo.
                If dtt.TableName = "OPTION" Then Continue For
                For Each dtr As DataRow In dtt.Rows
                    CreaArticolo(oCleArti, dtr, dtt.TableName)
                    nArticolo += 1
                    MostraAvanzamento("Gestione articoli: " & nArticolo.ToString() & " di " &
                                      nTotaleArticoli.ToString(), Percentuale(nArticolo, nTotaleArticoli))
                Next
            Next

            If Not InizializzaDBDIBA(oCleDiba, strMsg) Then Return False
            nCommessaImport = nCommessa
            If Not CreaDistinteProdotti(oCleDiba, dsImport) Then
                strMsg = "Errore durante la creazione delle distinte base."
                Return False
            End If

            Return True

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Function InizializzaMGARTI(ByRef oCleArti As CLEMGARTI, ByRef strMsg As String) As Boolean

        InizializzaMGARTI = False

        Try

            Dim strErr As String = ""
            Dim oTmp As Object = Nothing
            If Not NTSIstanziaDll(oApp.ServerDir, oApp.NetDir, "BNMGARTI", "BEMGARTI", oTmp, strErr, False, "", "") Then
                strMsg = strErr
                Return False
            End If
            oCleArti = CType(oTmp, CLEMGARTI)
            AddHandler oCleArti.RemoteEvent, AddressOf GestisciEventiFiliera
            Return oCleArti.Init(oApp, Nothing, CType(oApp.oMenu, CLE__MENU).oCleComm, "ARTVCO", False, "", "")

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Function InizializzaDBDIBA(ByRef oCleDiba As CLEDBDIBA, ByRef strMsg As String) As Boolean

        InizializzaDBDIBA = False

        Try

            Dim strErr As String = ""
            Dim oTmp As Object = Nothing
            If Not NTSIstanziaDll(oApp.ServerDir, oApp.NetDir, "BNDBDIBA", "BEDBDIBA", oTmp, strErr, False, "", "") Then
                strMsg = strErr
                Return False
            End If
            oCleDiba = CType(oTmp, CLEDBDIBA)
            AddHandler oCleDiba.RemoteEvent, AddressOf GestisciEventiFiliera
            If Not oCleDiba.Init(oApp, Nothing, CType(oApp.oMenu, CLE__MENU).oCleComm, "", False, "", "") Then Return False
            oCleDiba.strChild = ""
            oCleDiba.CaricaOpzReg()

            Return True

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function
#End Region

#Region "Articoli"
    Private Function CreaArticolo(ByVal oCleArti As CLEMGARTI, ByVal dtr As DataRow,
                                  ByVal strTipo As String) As Boolean

        CreaArticolo = False

        Try

            Dim strCodart As String = ""
            Dim strDescr As String = ""
            ComponiArticolo(dtr, strTipo, strCodart, strDescr)
            If String.IsNullOrWhiteSpace(strCodart) Then Return False

            Dim dtt As DataTable = Nothing
            oClhGsof.ValCodiceDb(strCodart, oApp.Ditta, "ARTICO", "S", "", dtt)
            If dtt IsNot Nothing AndAlso dtt.Rows.Count > 0 Then Return True

            Dim dsArti As New DataSet
            oCleArti.strCodartStd = strCodart
            oCleArti.strArtvCodroot = strCodart
            oCleArti.ArticoNuovo(dsArti)
            oCleArti.Nuovo(dsArti)
            oCleArti.dtrArtiRow = dsArti.Tables("ARTICO").Rows(0)
            With oCleArti.dtrArtiRow
                !ar_codart = strCodart
                !ar_descr = strDescr
                Select Case strTipo
                    Case "BAR", "BAR_OPTION", "OPTION"
                        !ar_unmis = "BAR"
                    Case "GLASS", "PANEL"
                        !ar_unmis = "MQ"
                    Case "FITMENT"
                        !ar_unmis = "PZ"
                    Case Else
                        !ar_unmis = strUnMisArticolo
                End Select
                If strTipo = "OPTION" Then !ar_unmis2 = "ML"
                !ar_codiva = nCodIvaArticolo
                !ar_controp = nContropArticolo
                !ar_controa = nControaArticolo
                If strTipo = "JOB" Then
                    !ar_gescomm = "O"
                    If Not String.IsNullOrWhiteSpace(strClasseProdottoFinito) Then !ar_codcla1 = strClasseProdottoFinito
                End If
            End With

            Return oCleArti.Salva(False)

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Protected Overridable Sub ComponiArticolo(ByVal dtr As DataRow, ByVal strTipo As String,
                                              ByRef strCodart As String, ByRef strDescr As String)

        Dim bCodiceFileSeparato As Boolean = False

        Try

            Select Case strTipo
                Case "JOB"
                    strCodart = NTSCStr(dtr!NomeCommessa) & "." & NTSCStr(dtr!NumeroVoce)
                    strDescr = NTSCStr(dtr!DesVoce)
                Case "BAR"
                    'TXB -> Barre: serie, codice e trattamento/colore.
                    strCodart = Get_Codart_Barre_FS(dtr)
                    strDescr = GetValoreCampoFileSeparato(dtr, 4)
                    bCodiceFileSeparato = True
                Case "BAR_OPTION"
                    strCodart = NTSCStr(dtr!Serie) & "." & NTSCStr(dtr!Codice)
                Case "FITMENT"
                    'TXF -> Accessori
                    strCodart = Get_Codart_Accessori_FS(dtr)
                    strDescr = GetValoreCampoFileSeparato(dtr, 8)
                    bCodiceFileSeparato = True
                Case "GLASS"
                    'TXG -> Vetri
                    strCodart = Get_Codart_Vetri_FS(dtr)
                    strDescr = GetValoreCampoFileSeparato(dtr, 7)
                    bCodiceFileSeparato = True
                Case "OPTION"
                    'TXO -> Profili
                    strCodart = Get_Codart_Profili_FS(dtr)
                    strDescr = GetValoreCampoFileSeparato(dtr, 4)
                    bCodiceFileSeparato = True
                Case "PANEL"
                    'TXP -> Pannelli
                    strCodart = Get_Codart_Pannelli_FS(dtr)
                    strDescr = GetValoreCampoFileSeparato(dtr, 7)
                    bCodiceFileSeparato = True
            End Select

            'Le funzioni di HHIMEX per i file separati conservano gli spazi.
            If Not bCodiceFileSeparato Then strCodart = strCodart.Replace(" ", "-")
            If strCodart.Length > CodartMaxLen Then strCodart = strCodart.Substring(0, CodartMaxLen)
            If String.IsNullOrWhiteSpace(strDescr) Then strDescr = strCodart
            If strDescr.Length > DesartMaxLen Then strDescr = strDescr.Substring(0, DesartMaxLen)

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub

    Protected Overridable Function GetValoreCampoFileSeparato(ByVal dtr As DataRow,
                                                               ByVal nNumeroCampo As Integer) As String

        GetValoreCampoFileSeparato = String.Empty

        Try

            If dtr Is Nothing OrElse nNumeroCampo < 1 Then Return ""
            If nNumeroCampo > dtr.ItemArray.Length Then Return ""
            GetValoreCampoFileSeparato = NTSCStr(dtr.ItemArray(nNumeroCampo - 1)).Trim()

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Public Overridable Function Get_Codart_Barre_FS(ByVal dtr As DataRow) As String

        Get_Codart_Barre_FS = "D"

        Try

            Get_Codart_Barre_FS = (GetValoreCampoFileSeparato(dtr, 2) & " " & GetValoreCampoFileSeparato(dtr, 3) & " " & GetValoreCampoFileSeparato(dtr, 6)).Trim()

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Public Overridable Function Get_Codart_Accessori_FS(ByVal dtr As DataRow) As String

        Get_Codart_Accessori_FS = String.Empty

        Try

            Get_Codart_Accessori_FS = GetValoreCampoFileSeparato(dtr, 7)

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Public Overridable Function Get_Codart_Vetri_FS(ByVal dtr As DataRow) As String

        Get_Codart_Vetri_FS = String.Empty

        Try

            Get_Codart_Vetri_FS = (GetValoreCampoFileSeparato(dtr, 6) & " " & GetValoreCampoFileSeparato(dtr, 8)).Trim()

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Public Overridable Function Get_Codart_Profili_FS(ByVal dtr As DataRow) As String

        Get_Codart_Profili_FS = String.Empty

        Try

            Get_Codart_Profili_FS = (GetValoreCampoFileSeparato(dtr, 2) & " " & GetValoreCampoFileSeparato(dtr, 3) & " " & GetValoreCampoFileSeparato(dtr, 6)).Trim()

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function

    Public Overridable Function Get_Codart_Pannelli_FS(ByVal dtr As DataRow) As String

        Get_Codart_Pannelli_FS = String.Empty

        Try
            Get_Codart_Pannelli_FS = GetValoreCampoFileSeparato(dtr, 6)
        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
            Return "D"
        End Try
    End Function

    Private Function CodiceArticolo(ByVal dtr As DataRow, ByVal strTipo As String) As String

        CodiceArticolo = ""

        Try

            Dim strDescr As String = ""
            ComponiArticolo(dtr, strTipo, CodiceArticolo, strDescr)

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function
#End Region

#Region "Distinte base"
    Private Function CreaDistinteProdotti(ByVal oCleDiba As CLEDBDIBA, ByVal dsImport As DataSet) As Boolean

        CreaDistinteProdotti = False

        Try

            Dim nDistinta As Integer = 0
            Dim nTotaleDistinte As Integer = dsImport.Tables("JOB").Rows.Count
            MostraAvanzamento("Gestione distinte base prodotti finiti...", 0)
            For Each dtrJob As DataRow In dsImport.Tables("JOB").Rows
                Dim strPadre As String = CodiceArticolo(dtrJob, "JOB")
                Dim dsMovdis As DataSet = ApriDistinta(oCleDiba, strPadre)
                If dsMovdis Is Nothing Then Return False
                Dim dDivisore As Decimal = NTSCDec(dtrJob!MoltiplicatoreStrutture)
                If dDivisore = 0D Then dDivisore = 1D

                AggiungiComponenti(oCleDiba, dsMovdis, dsImport, "GLASS", "NumeroVoce", NTSCStr(dtrJob!NumeroVoce), "NumeroPezzi", "GLASS", dDivisore)
                AggiungiComponenti(oCleDiba, dsMovdis, dsImport, "FITMENT", "NumeroVoce", NTSCStr(dtrJob!NumeroVoce), "Moltiplicatore", "FITMENT", dDivisore)
                AggiungiComponenti(oCleDiba, dsMovdis, dsImport, "PANEL", "NumeroVoce", NTSCStr(dtrJob!NumeroVoce), "NumeroPezzi", "PANEL", dDivisore)
                AggiungiProfiliDaTXO(oCleDiba, dsMovdis, dsImport,
                                    NTSCStr(dtrJob!NumeroVoce), dDivisore)

                If Not oCleDiba.Salva(False, "!" & strPadre & vbLf & vbTab & strPadre, "") Then Return False
                nDistinta += 1
                MostraAvanzamento("Gestione distinte base prodotti finiti: " & nDistinta.ToString() & " di " &
                                  nTotaleDistinte.ToString(), Percentuale(nDistinta, nTotaleDistinte))
            Next

            Return True

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Sub AggiungiComponenti(ByVal oCleDiba As CLEDBDIBA, ByVal dsMovdis As DataSet,
                                   ByVal dsImport As DataSet, ByVal strTabella As String,
                                   ByVal strCampo As String, ByVal strValore As String,
                                   ByVal strCampoQuant As String, ByVal strTipo As String,
                                   ByVal dDivisore As Decimal, Optional ByVal bLike As Boolean = False)

        Try

            If Not dsImport.Tables.Contains(strTabella) Then Return
            Dim strFiltro As String = strCampo & If(bLike, " LIKE ", " = ") & CStrSQL(strValore)
            For Each dtr As DataRow In dsImport.Tables(strTabella).Select(strFiltro)
                AggiungiFiglio(dsMovdis, CodiceArticolo(dtr, strTipo), NTSCDec(dtr(strCampoQuant)) / dDivisore, "PZ")
                oCleDiba.MovdisSalva(False)
            Next

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub

    Private Sub AggiungiProfiliDaTXO(ByVal oCleDiba As CLEDBDIBA, ByVal dsMovdis As DataSet,
                                     ByVal dsImport As DataSet, ByVal strNumeroVoce As String,
                                     ByVal dDivisore As Decimal)

        Try

            If Not dsImport.Tables.Contains("OPTION") OrElse
               Not dsImport.Tables.Contains("BAR") Then Return

            For Each dtrOpt As DataRow In dsImport.Tables("OPTION").Select(
                    "IDPezzo LIKE " & CStrSQL(strNumeroVoce & "-%"))
                Dim dtrBar As DataRow = TrovaBarraTXB(dsImport, dtrOpt)
                If dtrBar Is Nothing Then Continue For

                Dim dConsumo As Decimal = CalcolaConsumoBarre(dtrOpt)
                If dDivisore <> 0D Then dConsumo /= dDivisore
                AggiungiFiglio(dsMovdis, CodiceArticolo(dtrBar, "BAR"), dConsumo, "BAR")
                oCleDiba.MovdisSalva(False)
            Next

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub

    Private Function TrovaBarraTXB(ByVal dsImport As DataSet, ByVal dtrOpt As DataRow) As DataRow

        Try

            If dsImport Is Nothing OrElse dtrOpt Is Nothing OrElse
               Not dsImport.Tables.Contains("BAR") Then Return Nothing

            Dim strFiltro As String =
                "SerieProfilo = " & CStrSQL(NTSCStr(dtrOpt!Serie)) &
                " AND CodiceProfilo = " & CStrSQL(NTSCStr(dtrOpt!Codice))
            Dim righe As DataRow() = dsImport.Tables("BAR").Select(strFiltro)
            If righe.Length = 1 Then Return righe(0)

            If righe.Length > 1 Then
                Dim strColore As String = NTSCStr(dtrOpt!CodiceColoreESTERNO).Trim()
                If Not String.IsNullOrWhiteSpace(strColore) Then
                    Dim righeColore As DataRow() = dsImport.Tables("BAR").Select(
                        strFiltro & " AND CodiceColoreInterno = " & CStrSQL(strColore))
                    If righeColore.Length = 1 Then Return righeColore(0)
                End If
            End If

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

        Return Nothing

    End Function

    Private Function ValidaProfiliTXO(ByVal dsImport As DataSet, ByRef strMsg As String) As Boolean

        ValidaProfiliTXO = False

        Try

            If Not dsImport.Tables.Contains("OPTION") OrElse
               Not dsImport.Tables.Contains("BAR") Then
                strMsg = "I file TXO e TXB sono necessari per associare i tagli alle materie prime."
                Return False
            End If

            For Each dtrOpt As DataRow In dsImport.Tables("OPTION").Rows
                If TrovaBarraTXB(dsImport, dtrOpt) Is Nothing Then
                    strMsg = "Il profilo TXO " & NTSCStr(dtrOpt!Serie) & " " &
                             NTSCStr(dtrOpt!Codice) &
                             " non corrisponde in modo univoco a una materia prima del TXB."
                    Return False
                End If
                If CalcolaConsumoBarre(dtrOpt) <= 0D Then
                    strMsg = "Il profilo TXO " & NTSCStr(dtrOpt!Serie) & " " &
                             NTSCStr(dtrOpt!Codice) &
                             " contiene numero pezzi, lunghezza pezzo o lunghezza barra non validi."
                    Return False
                End If
            Next

            Return True

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Function CalcolaConsumoBarre(ByVal dtrOpt As DataRow) As Decimal

        Try

            Dim dLunghezzaBarra As Decimal = NTSCDec(dtrOpt!LunghezzaBarra)
            If dLunghezzaBarra <= 0D Then Return 0D
            Return NTSCDec(dtrOpt!NumeroPezzi) * NTSCDec(dtrOpt!MisuraAlettaEsterna) / dLunghezzaBarra

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

        Return 0D

    End Function

    Private Function ApriDistinta(ByVal oCleDiba As CLEDBDIBA, ByVal strPadre As String) As DataSet

        ApriDistinta = Nothing

        Try

            Dim dsDiba As New DataSet
            ImpostaDistinta(oCleDiba, strPadre)
            Dim bNuova As Boolean = Not oCleDiba.Apri(oApp.Ditta, strPadre, "", "", "", dsDiba)
            ImpostaDistinta(oCleDiba, strPadre)
            If bNuova AndAlso Not oCleDiba.NuovoStato0(dsDiba, strPadre) Then Return ApriDistinta

            Dim dsMovdis As New DataSet
            If Not oCleDiba.MovdisApri("!" & strPadre & vbLf & vbTab & strPadre, strPadre, strPadre,
                                      NTSCStr(Date.Now), "0", "0", "0", "R", dsMovdis, "", "", "") Then Return ApriDistinta
            Dim strNuovaVersione As String = CreaVersione(oCleDiba, strPadre)
            If String.IsNullOrWhiteSpace(strNuovaVersione) Then Return Nothing
            oCleDiba.dsShared.Tables("DISTBAS").Rows(0)!db_verscorr = strNuovaVersione

            ApriDistinta = dsMovdis

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Sub ImpostaDistinta(ByVal oCleDiba As CLEDBDIBA, ByVal strPadre As String)

        Try

            oCleDiba.strCodiceRoot = strPadre
            oCleDiba.strCodArtEsploso = strPadre
            oCleDiba.strDibaCoddb = strPadre
            oCleDiba.strFirstDibaCoddb = strPadre
            oCleDiba.strCiclopredef = ""
            oCleDiba.bDibaOpen = False

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub

    Private Function CreaVersione(ByVal oCleDiba As CLEDBDIBA, ByVal strPadre As String) As String

        CreaVersione = ""

        Try

            Dim dsNote As New DataSet
            With oCleDiba.dsShared.Tables("DISTBAS").Rows(0)
                oCleDiba.strDbnoCodDB = NTSCStr(!db_coddb)
                oCleDiba.strCodPadre = NTSCStr(!xx_codart)
                oCleDiba.lCommeca = nCommessaImport
            End With
            oCleDiba.DbnoApri(dsNote)
            oCleDiba.DbnoOnAddNew(dsNote)
            strVersioneImport = oCleDiba.CreaNuovaVersione(strPadre)
            dsNote.Tables("DBNOTE").Rows.Add()
            dsNote.Tables("DBNOTE").Rows(dsNote.Tables("DBNOTE").Rows.Count - 1)!dbn_vers = strVersioneImport

            If oCleDiba.DbnoSalva(False) Then CreaVersione = strVersioneImport

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Sub AggiungiFiglio(ByVal dsMovdis As DataSet, ByVal strCodart As String,
                               ByVal dQuant As Decimal, ByVal strUnMis As String)

        Try

            If String.IsNullOrWhiteSpace(strCodart) OrElse dQuant = 0D Then Return
            Dim righe As DataRow() = dsMovdis.Tables("MOVDIS").Select("md_codfigli = " & CStrSQL(strCodart) &
                " AND md_davers = " & CStrSQL(strVersioneImport) & " AND md_avers = " & CStrSQL(strVersioneImport))
            Dim dtr As DataRow
            If righe.Length = 0 Then
                dtr = dsMovdis.Tables("MOVDIS").NewRow()
                dsMovdis.Tables("MOVDIS").Rows.Add(dtr)
            Else
                dtr = righe(0)
            End If
            dtr!md_codfigli = strCodart
            dtr!md_unmis = strUnMis
            dtr!md_quant = NTSCDec(dtr!md_quant) + dQuant
            dtr!md_davers = strVersioneImport
            dtr!md_avers = strVersioneImport

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub
#End Region

#Region "Fabbisogni materiali"
    Private Function CreaFabbisogniMateriali(ByVal dsImport As DataSet) As DataTable

        CreaFabbisogniMateriali = New DataTable("MATERIALI")

        Try
            CreaFabbisogniMateriali.Columns.Add("codart", GetType(String))
            CreaFabbisogniMateriali.Columns.Add("descr", GetType(String))
            CreaFabbisogniMateriali.Columns.Add("quant", GetType(Decimal))
            For Each dtrOfferta As DataRow In RigheOffertaAttive()
                Dim dQuantitaOfferta As Decimal = NTSCDec(dtrOfferta!ec_quant)
                If dQuantitaOfferta = 0D Then Continue For

                Dim strNumeroVoce As String = NTSCStr(dtrOfferta!ec_hhnumvoce).Trim()
                Dim righeJob As DataRow() = TrovaRigheJob(dsImport, strNumeroVoce)
                If righeJob.Length = 0 Then Continue For

                Dim dtrJob As DataRow = righeJob(0)
                Dim strVoce As String = NTSCStr(dtrJob!NumeroVoce)
                Dim dQuantitaOriginale As Decimal = NTSCDec(dtrJob!MoltiplicatoreStrutture)
                If dQuantitaOriginale = 0D Then dQuantitaOriginale = 1D
                Dim dFattore As Decimal = dQuantitaOfferta / dQuantitaOriginale

                AccumulaDaTabella(CreaFabbisogniMateriali, dsImport, "GLASS", "NumeroVoce", strVoce, "NumeroPezzi", "GLASS", dFattore)
                AccumulaDaTabella(CreaFabbisogniMateriali, dsImport, "FITMENT", "NumeroVoce", strVoce, "Moltiplicatore", "FITMENT", dFattore)
                AccumulaDaTabella(CreaFabbisogniMateriali, dsImport, "PANEL", "NumeroVoce", strVoce, "NumeroPezzi", "PANEL", dFattore)
                If dsImport.Tables.Contains("OPTION") Then
                    For Each dtr As DataRow In dsImport.Tables("OPTION").Select("IDPezzo LIKE " & CStrSQL(strVoce & "-%"))
                        Dim dtrBar As DataRow = TrovaBarraTXB(dsImport, dtr)
                        If dtrBar Is Nothing Then Continue For
                        AccumulaFabbisogno(CreaFabbisogniMateriali, CodiceArticolo(dtrBar, "BAR"),
                                          NTSCStr(dtrBar!DescProfilo),
                                          CalcolaConsumoBarre(dtr) * dFattore)
                    Next
                End If
            Next

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Sub AccumulaDaTabella(ByVal dttOut As DataTable, ByVal dsImport As DataSet,
                                  ByVal strTabella As String, ByVal strCampo As String,
                                  ByVal strValore As String, ByVal strCampoQuant As String,
                                  ByVal strTipo As String, ByVal dFattore As Decimal)

        Try

            If Not dsImport.Tables.Contains(strTabella) Then Return
            For Each dtr As DataRow In dsImport.Tables(strTabella).Select(strCampo & " = " & CStrSQL(strValore))
                AccumulaFabbisogno(dttOut, CodiceArticolo(dtr, strTipo),
                                  NTSCStr(dtr!Descrizione), NTSCDec(dtr(strCampoQuant)) * dFattore)
            Next

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub

    Private Sub AccumulaFabbisogno(ByVal dttOut As DataTable, ByVal strCodart As String,
                                   ByVal strDescr As String, ByVal dQuant As Decimal)

        Try

            If String.IsNullOrWhiteSpace(strCodart) OrElse dQuant = 0D Then Return
            Dim righe As DataRow() = dttOut.Select("codart = " & CStrSQL(strCodart))
            If righe.Length = 0 Then
                dttOut.Rows.Add(strCodart, strDescr, dQuant)
            Else
                righe(0)!quant = NTSCDec(righe(0)!quant) + dQuant
            End If

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Sub
#End Region
#End Region

End Class
