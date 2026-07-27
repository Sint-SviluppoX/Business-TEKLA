Imports System.Data
Imports System.Data.Common
Imports NTSInformatica.CLN__STD
Imports System.IO
Imports Microsoft.VisualBasic.FileIO

Imports System
Public Class CLHCRGSOF
  Inherits CLDCRGSOF

    Public Overridable Function RisolviCodicePacchetto(ByVal strFolderPath As String,
                                                       ByVal strCodicePreferito As String) As String
        RisolviCodicePacchetto = ""

        Try

            If Not Directory.Exists(strFolderPath) Then Return ""

            Dim files As String() = Directory.GetFiles(strFolderPath, "*_TXJ*.csv")
            If files.Length = 0 Then Return ""

            For Each strFile As String In files
                Dim strNome As String = Path.GetFileName(strFile)
                Dim nPos As Integer = strNome.IndexOf("_TXJ", StringComparison.OrdinalIgnoreCase)
                If nPos <= 0 Then Continue For

                Dim strCodice As String = strNome.Substring(0, nPos)
                If String.Equals(strCodice, strCodicePreferito, StringComparison.OrdinalIgnoreCase) Then Return strCodice
            Next

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Public Overridable Function CreaRestituisciCommessa(ByVal strDescrizione As String) As Integer

        CreaRestituisciCommessa = 0

        Try

            Dim dtt As DataTable = OpenRecordset(
            "SELECT TOP 1 co_comme FROM commess " &
            $"WHERE codditt = {CStrSQL(oApp.Ditta)} AND LTRIM(RTRIM(co_descr1)) = LTRIM(RTRIM({CStrSQL(strDescrizione)})) " &
            "ORDER BY co_comme", CLE__APP.DBTIPO.DBAZI)

            If dtt IsNot Nothing AndAlso dtt.Rows.Count > 0 Then Return NTSCInt(dtt.Rows(0)!co_comme)

            dtt = OpenRecordset(
            $"SELECT ISNULL(MAX(co_comme), 0) + 1 AS codice FROM commess WHERE codditt = {CStrSQL(oApp.Ditta)}",
            CLE__APP.DBTIPO.DBAZI)
            If dtt Is Nothing OrElse dtt.Rows.Count = 0 Then Return 0

            Dim nCodice As Integer = NTSCInt(dtt.Rows(0)!codice)
            Dim strSQL As String =
            "INSERT INTO commess (codditt, co_comme, co_conto, co_descr1, co_descr2, co_dtaper, co_dtagg, co_dtscad, co_stato, co_chiusa, co_opnome, co_ultagg) " &
            $"VALUES ({CStrSQL(oApp.Ditta)}, {CStrSQL(nCodice)}, 0, {CStrSQL(strDescrizione)}, '', GETDATE(), GETDATE(), " &
            "DATEFROMPARTS(YEAR(GETDATE()), 12, 31), '1', 'N', " & CStrSQL(oApp.User.Nome) & ", GETDATE())"

            If Execute(strSQL, CLE__APP.DBTIPO.DBAZI) = 0 Then Return 0
            Return nCodice

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Public Overridable Function EsistonoImpegni(ByVal nCommessa As Integer) As Boolean

        EsistonoImpegni = False

        Try

            Dim dtt As DataTable = OpenRecordset(
            "SELECT TOP 1 td_numord FROM testord " &
            $"WHERE codditt = {CStrSQL(oApp.Ditta)} AND td_commeca = {CStrSQL(nCommessa)} AND td_tipork IN ('R', '#', 'H')",
            CLE__APP.DBTIPO.DBAZI)

            Return dtt IsNot Nothing AndAlso dtt.Rows.Count > 0

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Public Overridable Function EsisteImpegno(ByVal nCommessa As Integer, ByVal strTipork As String) As Boolean

        EsisteImpegno = False

        Try
            Dim dtt As DataTable = OpenRecordset(
            "SELECT TOP 1 td_numord FROM testord " &
            $"WHERE codditt = {CStrSQL(oApp.Ditta)} AND td_commeca = {CStrSQL(nCommessa)} " &
            $"AND td_tipork = {CStrSQL(strTipork)}",
            CLE__APP.DBTIPO.DBAZI)

            Return dtt IsNot Nothing AndAlso dtt.Rows.Count > 0

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Public Overridable Function RisolviContoCliente(ByVal nCodLead As Integer,
                                                    ByVal nContoDefault As Integer) As Integer
        RisolviContoCliente = nContoDefault

        Try

            Dim dtt As DataTable = OpenRecordset(
            "SELECT TOP 1 le_conto FROM leads " &
            $"WHERE codditt = {CStrSQL(oApp.Ditta)} AND le_codlead = {CStrSQL(nCodLead)} " &
            "AND le_conto > 0 ORDER BY le_codleaddest",
            CLE__APP.DBTIPO.DBAZI)

            If dtt IsNot Nothing AndAlso dtt.Rows.Count > 0 Then Return NTSCInt(dtt.Rows(0)!le_conto)
            Return nContoDefault

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Public Overridable Function CaricaPacchettoCSV(ByVal strFolderPath As String,
                                                  ByVal strCodice As String) As DataSet
        CaricaPacchettoCSV = New DataSet

        Try

            Dim schemi As New Dictionary(Of String, String()) From {
                {"JOB", {"NomeCommessa", "DesCommessa", "NumeroVoce", "Etichetta", "DesVoce", "MoltiplicatoreStrutture"}},
                {"BAR", {"NomeCommessa", "SerieProfilo", "CodiceProfilo", "DescProfilo",
                         "TipoColoreInterno", "CodiceColoreInterno", "DescrizioneColoreInterno",
                         "TipoColoreEsterno", "CodiceColoreEsterno", "DescrizioneColoreEsterno",
                         "Multibarra", "LunghezzaBarra", "QuantitaBarre", "PercentualeUtilizzo",
                         "PercentualeScarto", "Peso", "Superficie", "Classe",
                         "CampoLibero", "DataEsportazione", "NoName1"}},
                {"GLASS", {"NomeCommessa", "NumeroVoce", "Etichetta", "Classe", "Serie", "Codice", "Descrizione", "TipoColore", "CodiceColore", "DesColore", "NumeroPezzi"}},
                {"FITMENT", {"NomeCommessa", "NumeroVoce", "Etichetta", "Classe", "Marca", "Serie", "Codice", "Descrizione", "TipoColore", "CodColore", "DesColore", "Categoria", "Tipo", "UnitaVendita", "AccessorioTrattato", "Moltiplicatore"}},
                {"PANEL", {"NomeCommessa", "NumeroVoce", "Etichetta", "Classe", "Serie", "Codice", "Descrizione", "TipoColore", "CodiceColore", "DesColore", "NumeroPezzi"}},
                {"OPTION", {"NomeCommessa", "Serie", "Codice", "Descrizione", "TipoColoreESTERNO", "CodiceColoreESTERNO", "DescrizioneColoreESTERNO", "TipoColoreINTERNO", "CodiceColoreINTERNO", "DescrizioneColoreINTERNO", "Multibarra", "LunghezzaBarra", "MoltiplicatoreBarreUguali", "IDBarra", "NumeroVoce", "Etichetta", "Classe", "NumeroPezzi", "MisuraAlettaEsterna", "AngolazioneTaglioSinistro", "AngolazioneTaglioDestro", "AlettaInterna", "Camera", "AlettaEsterna", "NoName1", "IDPezzo"}}
            }
            Dim suffissi As New Dictionary(Of String, String) From {
                {"JOB", LeggiImpostazione("NomeFileCsv_Job", "TXJ")},
                {"BAR", LeggiImpostazione("NomeFileCsv_Bar", "TXB")},
                {"GLASS", LeggiImpostazione("NomeFileCsv_Glass", "TXG")},
                {"FITMENT", LeggiImpostazione("NomeFileCsv_Fitment", "TXF")},
                {"PANEL", LeggiImpostazione("NomeFileCsv_Panel", "TXP")},
                {"OPTION", LeggiImpostazione("NomeFileCsv_Option", "TXO")}
            }
            Dim strSeparatore As String = LeggiImpostazione("CharSepNomeCsv", "_")

            For Each coppia As KeyValuePair(Of String, String) In suffissi
                Dim strFile As String = RisolviFileCSV(strFolderPath, strCodice & strSeparatore & coppia.Value)
                Dim dtt As DataTable = LeggiCSV(strFile, coppia.Key, schemi(coppia.Key), strCodice)
                If dtt IsNot Nothing Then CaricaPacchettoCSV.Tables.Add(dtt)
            Next

            If Not CaricaPacchettoCSV.Tables.Contains("JOB") OrElse
               Not CaricaPacchettoCSV.Tables.Contains("OPTION") OrElse
               CaricaPacchettoCSV.Tables("JOB").Rows.Count = 0 OrElse
               CaricaPacchettoCSV.Tables("OPTION").Rows.Count = 0 Then CaricaPacchettoCSV = Nothing

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Public Overridable Function LeggiImpostazione(ByVal strNome As String,
                                                  ByVal strDefault As String) As String
        LeggiImpostazione = strDefault

        Try

            LeggiImpostazione = NTSCStr(GetSettingBus("BSHHIMDB", "OPZIONI", ".", strNome,
                                                      strDefault, " ", strDefault))
        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Function RisolviFileCSV(ByVal strFolderPath As String, ByVal strNomeBase As String) As String

        RisolviFileCSV = ""

        Try

            Dim files As String() = Directory.GetFiles(strFolderPath, strNomeBase & "*.csv")
            If files.Length = 1 Then RisolviFileCSV = files(0)

        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try

    End Function

    Private Function LeggiCSV(ByVal strFile As String, ByVal strTabella As String,
                              ByVal campi As String(), ByVal strCodiceDaNomeFile As String) As DataTable
        LeggiCSV = Nothing

        Try

            If String.IsNullOrWhiteSpace(strFile) OrElse Not File.Exists(strFile) Then Return Nothing

            Dim dtt As New DataTable(strTabella)
            For Each strCampo As String In campi
                dtt.Columns.Add(strCampo, GetType(String))
            Next

            Using parser As New TextFieldParser(strFile)
                parser.TextFieldType = FieldType.Delimited
                parser.SetDelimiters(";")
                parser.HasFieldsEnclosedInQuotes = True
                Dim bPrimaRigaSignificativa As Boolean = True
                While Not parser.EndOfData
                    Dim valori As String() = parser.ReadFields()
                    If valori Is Nothing OrElse valori.Length = 0 Then Continue While
                    If bPrimaRigaSignificativa Then
                        bPrimaRigaSignificativa = False
                        If CheckIntestazione(valori) Then Continue While
                    End If

                    Dim dtr As DataRow = dtt.NewRow()
                    For i As Integer = 0 To Math.Min(valori.Length, campi.Length) - 1
                        dtr(i) = valori(i)
                    Next
                    'F1/NomeCommessa deve essere ricavato dal prefisso del nome file
                    '<commessa>_<suffisso>.CSV e non dal contenuto della prima colonna.
                    If dtt.Columns.Count > 0 Then dtr(0) = strCodiceDaNomeFile
                    dtt.Rows.Add(dtr)
                End While
            End Using

            LeggiCSV = dtt

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
                Dim strValore As String = NTSCStr(valore).Trim().ToUpperInvariant().
                    Replace(" ", "").Replace(vbTab, "").Replace("/", "").
                    Replace("-", "").Replace("_", "")
                If Array.IndexOf(paroleIntestazione, strValore) >= 0 Then nCorrispondenze += 1
            Next

            Return nCorrispondenze >= 2

        Catch ex As Exception
            CLN__STD.GestErr(ex, Me, "")
        End Try

    End Function
End Class
