Public Class FormLegendas
    Public args As String
    Private Sub btOkleg_Click(sender As Object, e As EventArgs) Handles btOkleg.Click
        If cmbLegendas.SelectedItem IsNot Nothing Then
            Dim idiomaSelecionado As String = cmbLegendas.SelectedItem.ToString()
            If idiomaSelecionado.StartsWith("auto") Then
                args = "--write-auto-subs --sub-langs pt --sub-format srt --embed-subs "
            Else
                args = $"--write-sub --sub-langs ""{idiomaSelecionado}"" --sub-format srt --embed-subs "
            End If
        End If
        Me.DialogResult = DialogResult.OK
        Me.Close()
    End Sub

End Class