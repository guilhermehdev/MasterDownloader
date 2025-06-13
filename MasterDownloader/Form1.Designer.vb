<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        components = New ComponentModel.Container()
        txtUrl = New TextBox()
        btnAdicionar = New Button()
        btnExecutar = New Button()
        txtLog = New TextBox()
        lstLinks = New ListBox()
        btLimparLista = New Button()
        progressBarDownload = New ProgressBar()
        CheckBoxAudio = New CheckBox()
        StatusStrip1 = New StatusStrip()
        StatusLabel = New ToolStripStatusLabel()
        btLog = New Button()
        chkLegendas = New CheckBox()
        MenuStrip1 = New MenuStrip()
        ConfoguraçõesToolStripMenuItem = New ToolStripMenuItem()
        ImportarCookiesPrivadosToolStripMenuItem = New ToolStripMenuItem()
        AlterarPastaDestinoToolStripMenuItem = New ToolStripMenuItem()
        btCancelar = New Button()
        timerFakeProgress = New Timer(components)
        StatusStrip1.SuspendLayout()
        MenuStrip1.SuspendLayout()
        SuspendLayout()
        ' 
        ' txtUrl
        ' 
        txtUrl.Location = New Point(12, 30)
        txtUrl.Name = "txtUrl"
        txtUrl.PlaceholderText = "URL do vídeo ou playlist"
        txtUrl.Size = New Size(440, 23)
        txtUrl.TabIndex = 0
        ' 
        ' btnAdicionar
        ' 
        btnAdicionar.BackColor = Color.YellowGreen
        btnAdicionar.Cursor = Cursors.Hand
        btnAdicionar.FlatAppearance.BorderSize = 0
        btnAdicionar.FlatStyle = FlatStyle.Flat
        btnAdicionar.ForeColor = Color.White
        btnAdicionar.Location = New Point(458, 30)
        btnAdicionar.Name = "btnAdicionar"
        btnAdicionar.Size = New Size(45, 23)
        btnAdicionar.TabIndex = 2
        btnAdicionar.Text = "Add"
        btnAdicionar.UseVisualStyleBackColor = False
        ' 
        ' btnExecutar
        ' 
        btnExecutar.BackColor = Color.SteelBlue
        btnExecutar.Cursor = Cursors.Hand
        btnExecutar.FlatAppearance.BorderSize = 0
        btnExecutar.FlatStyle = FlatStyle.Flat
        btnExecutar.ForeColor = Color.White
        btnExecutar.Location = New Point(207, 187)
        btnExecutar.Name = "btnExecutar"
        btnExecutar.Size = New Size(102, 30)
        btnExecutar.TabIndex = 3
        btnExecutar.Text = "Download"
        btnExecutar.UseVisualStyleBackColor = False
        ' 
        ' txtLog
        ' 
        txtLog.Location = New Point(12, 222)
        txtLog.Multiline = True
        txtLog.Name = "txtLog"
        txtLog.ReadOnly = True
        txtLog.ScrollBars = ScrollBars.Vertical
        txtLog.Size = New Size(491, 176)
        txtLog.TabIndex = 9
        txtLog.Visible = False
        ' 
        ' lstLinks
        ' 
        lstLinks.FormattingEnabled = True
        lstLinks.ItemHeight = 15
        lstLinks.Location = New Point(12, 59)
        lstLinks.Name = "lstLinks"
        lstLinks.Size = New Size(491, 124)
        lstLinks.TabIndex = 12
        ' 
        ' btLimparLista
        ' 
        btLimparLista.BackColor = Color.IndianRed
        btLimparLista.Cursor = Cursors.Hand
        btLimparLista.FlatAppearance.BorderSize = 0
        btLimparLista.FlatStyle = FlatStyle.Flat
        btLimparLista.ForeColor = Color.White
        btLimparLista.Location = New Point(413, 187)
        btLimparLista.Name = "btLimparLista"
        btLimparLista.Size = New Size(90, 30)
        btLimparLista.TabIndex = 13
        btLimparLista.Text = "Limpar lista "
        btLimparLista.UseVisualStyleBackColor = False
        ' 
        ' progressBarDownload
        ' 
        progressBarDownload.Location = New Point(12, 404)
        progressBarDownload.Name = "progressBarDownload"
        progressBarDownload.Size = New Size(491, 23)
        progressBarDownload.Style = ProgressBarStyle.Continuous
        progressBarDownload.TabIndex = 14
        ' 
        ' CheckBoxAudio
        ' 
        CheckBoxAudio.AutoSize = True
        CheckBoxAudio.Location = New Point(12, 185)
        CheckBoxAudio.Name = "CheckBoxAudio"
        CheckBoxAudio.Size = New Size(100, 19)
        CheckBoxAudio.TabIndex = 15
        CheckBoxAudio.Text = "Somente mp3"
        CheckBoxAudio.UseVisualStyleBackColor = True
        ' 
        ' StatusStrip1
        ' 
        StatusStrip1.Items.AddRange(New ToolStripItem() {StatusLabel})
        StatusStrip1.Location = New Point(0, 438)
        StatusStrip1.Name = "StatusStrip1"
        StatusStrip1.Size = New Size(516, 22)
        StatusStrip1.TabIndex = 16
        StatusStrip1.Text = "StatusStrip1"
        ' 
        ' StatusLabel
        ' 
        StatusLabel.Name = "StatusLabel"
        StatusLabel.Size = New Size(39, 17)
        StatusLabel.Text = "Status"
        ' 
        ' btLog
        ' 
        btLog.Cursor = Cursors.Hand
        btLog.Location = New Point(121, 187)
        btLog.Name = "btLog"
        btLog.Size = New Size(80, 30)
        btLog.TabIndex = 17
        btLog.Text = "Exibir Log"
        btLog.UseVisualStyleBackColor = True
        ' 
        ' chkLegendas
        ' 
        chkLegendas.AutoSize = True
        chkLegendas.Location = New Point(12, 202)
        chkLegendas.Name = "chkLegendas"
        chkLegendas.Size = New Size(108, 19)
        chkLegendas.TabIndex = 18
        chkLegendas.Text = "Baixar legendas"
        chkLegendas.UseVisualStyleBackColor = True
        ' 
        ' MenuStrip1
        ' 
        MenuStrip1.Items.AddRange(New ToolStripItem() {ConfoguraçõesToolStripMenuItem})
        MenuStrip1.Location = New Point(0, 0)
        MenuStrip1.Name = "MenuStrip1"
        MenuStrip1.Size = New Size(516, 24)
        MenuStrip1.TabIndex = 19
        MenuStrip1.Text = "MenuStrip1"
        ' 
        ' ConfoguraçõesToolStripMenuItem
        ' 
        ConfoguraçõesToolStripMenuItem.DropDownItems.AddRange(New ToolStripItem() {ImportarCookiesPrivadosToolStripMenuItem, AlterarPastaDestinoToolStripMenuItem})
        ConfoguraçõesToolStripMenuItem.Name = "ConfoguraçõesToolStripMenuItem"
        ConfoguraçõesToolStripMenuItem.Size = New Size(96, 20)
        ConfoguraçõesToolStripMenuItem.Text = "Configurações"
        ' 
        ' ImportarCookiesPrivadosToolStripMenuItem
        ' 
        ImportarCookiesPrivadosToolStripMenuItem.Name = "ImportarCookiesPrivadosToolStripMenuItem"
        ImportarCookiesPrivadosToolStripMenuItem.Size = New Size(211, 22)
        ImportarCookiesPrivadosToolStripMenuItem.Text = "Importar cookies privados"
        ' 
        ' AlterarPastaDestinoToolStripMenuItem
        ' 
        AlterarPastaDestinoToolStripMenuItem.Name = "AlterarPastaDestinoToolStripMenuItem"
        AlterarPastaDestinoToolStripMenuItem.Size = New Size(211, 22)
        AlterarPastaDestinoToolStripMenuItem.Text = "Alterar pasta destino"
        ' 
        ' btCancelar
        ' 
        btCancelar.BackColor = Color.DarkOrange
        btCancelar.Cursor = Cursors.Hand
        btCancelar.Enabled = False
        btCancelar.FlatAppearance.BorderSize = 0
        btCancelar.FlatStyle = FlatStyle.Flat
        btCancelar.ForeColor = Color.White
        btCancelar.Location = New Point(310, 187)
        btCancelar.Name = "btCancelar"
        btCancelar.Size = New Size(102, 30)
        btCancelar.TabIndex = 20
        btCancelar.Text = "Cancelar"
        btCancelar.UseVisualStyleBackColor = False
        ' 
        ' timerFakeProgress
        ' 
        ' 
        ' Form1
        ' 
        ClientSize = New Size(516, 460)
        Controls.Add(btCancelar)
        Controls.Add(chkLegendas)
        Controls.Add(btLog)
        Controls.Add(StatusStrip1)
        Controls.Add(MenuStrip1)
        Controls.Add(CheckBoxAudio)
        Controls.Add(progressBarDownload)
        Controls.Add(btLimparLista)
        Controls.Add(lstLinks)
        Controls.Add(txtUrl)
        Controls.Add(btnAdicionar)
        Controls.Add(btnExecutar)
        Controls.Add(txtLog)
        FormBorderStyle = FormBorderStyle.FixedSingle
        MainMenuStrip = MenuStrip1
        MaximizeBox = False
        Name = "Form1"
        StartPosition = FormStartPosition.CenterScreen
        Text = "MasterDownloader"
        StatusStrip1.ResumeLayout(False)
        StatusStrip1.PerformLayout()
        MenuStrip1.ResumeLayout(False)
        MenuStrip1.PerformLayout()
        ResumeLayout(False)
        PerformLayout()
    End Sub

    Private WithEvents txtUrl As TextBox
    Private WithEvents btnAdicionar As Button
    Private WithEvents btnExecutar As Button
    Private WithEvents txtLog As TextBox
    Friend WithEvents lstLinks As ListBox
    Private WithEvents btLimparLista As Button
    Friend WithEvents progressBarDownload As ProgressBar
    Friend WithEvents CheckBoxAudio As CheckBox
    Friend WithEvents StatusStrip1 As StatusStrip
    Friend WithEvents btLog As Button
    Friend WithEvents chkLegendas As CheckBox
    Friend WithEvents MenuStrip1 As MenuStrip
    Friend WithEvents ConfoguraçõesToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents ImportarCookiesPrivadosToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents AlterarPastaDestinoToolStripMenuItem As ToolStripMenuItem
    Private WithEvents btCancelar As Button
    Friend WithEvents StatusLabel As ToolStripStatusLabel
    Friend WithEvents timerFakeProgress As Timer
End Class
