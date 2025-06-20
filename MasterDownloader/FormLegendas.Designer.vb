<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class FormLegendas
    Inherits System.Windows.Forms.Form

    'Descartar substituições de formulário para limpar a lista de componentes.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Exigido pelo Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'OBSERVAÇÃO: o procedimento a seguir é exigido pelo Windows Form Designer
    'Pode ser modificado usando o Windows Form Designer.  
    'Não o modifique usando o editor de códigos.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Dim resources As System.ComponentModel.ComponentResourceManager = New System.ComponentModel.ComponentResourceManager(GetType(FormLegendas))
        GroupBox1 = New GroupBox()
        btOkleg = New Button()
        cmbLegendas = New ComboBox()
        GroupBox1.SuspendLayout()
        SuspendLayout()
        ' 
        ' GroupBox1
        ' 
        GroupBox1.Controls.Add(btOkleg)
        GroupBox1.Controls.Add(cmbLegendas)
        GroupBox1.ForeColor = Color.White
        GroupBox1.Location = New Point(12, 12)
        GroupBox1.Name = "GroupBox1"
        GroupBox1.Size = New Size(285, 63)
        GroupBox1.TabIndex = 0
        GroupBox1.TabStop = False
        GroupBox1.Text = "Legendas disponíveis"
        ' 
        ' btOkleg
        ' 
        btOkleg.ForeColor = Color.Black
        btOkleg.Location = New Point(241, 21)
        btOkleg.Name = "btOkleg"
        btOkleg.Size = New Size(38, 23)
        btOkleg.TabIndex = 1
        btOkleg.Text = "OK"
        btOkleg.UseVisualStyleBackColor = True
        ' 
        ' cmbLegendas
        ' 
        cmbLegendas.FormattingEnabled = True
        cmbLegendas.Location = New Point(15, 22)
        cmbLegendas.Name = "cmbLegendas"
        cmbLegendas.Size = New Size(220, 23)
        cmbLegendas.TabIndex = 0
        ' 
        ' FormLegendas
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        BackColor = Color.SteelBlue
        ClientSize = New Size(309, 89)
        Controls.Add(GroupBox1)
        FormBorderStyle = FormBorderStyle.None
        Icon = CType(resources.GetObject("$this.Icon"), Icon)
        Name = "FormLegendas"
        StartPosition = FormStartPosition.CenterScreen
        Text = "Selecionar idioma"
        GroupBox1.ResumeLayout(False)
        ResumeLayout(False)
    End Sub

    Friend WithEvents GroupBox1 As GroupBox
    Friend WithEvents cmbLegendas As ComboBox
    Friend WithEvents btOkleg As Button
End Class
