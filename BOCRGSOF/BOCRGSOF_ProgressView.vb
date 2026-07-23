Imports System.Windows.Forms
Imports NTSInformatica.CLN__STD

Partial Public Class BOCRGSOF_ProgressView

    Public oCallParams As CLE__CLDP
    Public oClfGsof As CLFCRGSOF

    Public Overloads Function Init(ByRef Menu As CLE__MENU, ByRef Param As CLE__CLDP,
                                   Optional ByVal Ditta As String = "",
                                   Optional ByRef SharedControls As CLE__EVNT = Nothing) As Boolean
        Init = False
        Try
            oMenu = Menu
            oApp = oMenu.App
            oCallParams = Param
            DittaCorrente = If(Ditta <> "", Ditta, oApp.Ditta)
            GctlTipoDoc = ""

            MinimumSize = Size
            Init = True
        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try
    End Function

    Public Overridable Sub InitEntity(ByRef oCleIn As CLFCRGSOF)
        Try
            oClfGsof = oCleIn
            AddHandler oClfGsof.AvanzamentoFiliera, AddressOf Aggiorna
        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try
    End Sub

    Public Sub Aggiorna(ByVal strMessaggio As String, ByVal nPercentuale As Integer)
        Try
            lbProgress.Text = strMessaggio
            pbProgress.EditValue = Math.Max(0, Math.Min(100, nPercentuale))
            lbProgress.Refresh()
            pbProgress.Refresh()
            Application.DoEvents()
        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try
    End Sub

    Private Sub BOCRGSOF_ProgressView_FormClosed(ByVal sender As Object, ByVal e As FormClosedEventArgs) Handles Me.FormClosed
        Try
            If oClfGsof IsNot Nothing Then RemoveHandler oClfGsof.AvanzamentoFiliera, AddressOf Aggiorna
        Catch ex As Exception
            '-------------------------------------------------
            CLN__STD.GestErr(ex, Me, "")
            '-------------------------------------------------
        End Try
    End Sub
End Class
