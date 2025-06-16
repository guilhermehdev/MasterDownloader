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
        Button1 = New Button()
        ToolTip1 = New ToolTip(components)
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
        btnAdicionar.BackColor = Color.DeepSkyBlue
        btnAdicionar.Cursor = Cursors.Hand
        btnAdicionar.FlatAppearance.BorderColor = Color.SteelBlue
        btnAdicionar.FlatAppearance.MouseOverBackColor = Color.SteelBlue
        btnAdicionar.FlatStyle = FlatStyle.Flat
        btnAdicionar.ForeColor = Color.White
        btnAdicionar.Location = New Point(458, 30)
        btnAdicionar.Name = "btnAdicionar"
        btnAdicionar.Size = New Size(45, 23)
        btnAdicionar.TabIndex = 2
        btnAdicionar.Text = "Add"
        ToolTip1.SetToolTip(btnAdicionar, "Adiciona à fila e valida o link")
        btnAdicionar.UseVisualStyleBackColor = False
        ' 
        ' btnExecutar
        ' 
        btnExecutar.BackColor = Color.YellowGreen
        btnExecutar.Cursor = Cursors.Hand
        btnExecutar.FlatAppearance.BorderColor = Color.Green
        btnExecutar.FlatAppearance.MouseOverBackColor = Color.Green
        btnExecutar.FlatStyle = FlatStyle.Flat
        btnExecutar.ForeColor = Color.White
        btnExecutar.Location = New Point(207, 187)
        btnExecutar.Name = "btnExecutar"
        btnExecutar.Size = New Size(113, 30)
        btnExecutar.TabIndex = 3
        btnExecutar.Text = "Download"
        ToolTip1.SetToolTip(btnExecutar, "Inicia o download")
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
        btLimparLista.BackColor = Color.Orange
        btLimparLista.Cursor = Cursors.Hand
        btLimparLista.FlatAppearance.BorderColor = Color.DarkOrange
        btLimparLista.FlatAppearance.MouseOverBackColor = Color.DarkOrange
        btLimparLista.FlatStyle = FlatStyle.Flat
        btLimparLista.ForeColor = Color.White
        btLimparLista.Location = New Point(392, 187)
        btLimparLista.Name = "btLimparLista"
        btLimparLista.Size = New Size(60, 30)
        btLimparLista.TabIndex = 13
        btLimparLista.Text = "Limpar "
        ToolTip1.SetToolTip(btLimparLista, "Esvazia a lista de links")
        btLimparLista.UseVisualStyleBackColor = False
        ' 
        ' progressBarDownload
        ' 
        progressBarDownload.Location = New Point(12, 403)
        progressBarDownload.Name = "progressBarDownload"
        progressBarDownload.Size = New Size(491, 46)
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
        StatusStrip1.Location = New Point(0, 458)
        StatusStrip1.Name = "StatusStrip1"
        StatusStrip1.Size = New Size(516, 22)
        StatusStrip1.TabIndex = 16
        StatusStrip1.Text = "StatusStrip1"
        ' 
        ' StatusLabel
        ' 
        StatusLabel.Name = "StatusLabel"
        StatusLabel.Size = New Size(120, 17)
        StatusLabel.Text = "Status: Aguardando..."
        ' 
        ' btLog
        ' 
        btLog.Cursor = Cursors.Hand
        btLog.FlatAppearance.BorderColor = Color.DarkGray
        btLog.FlatAppearance.MouseOverBackColor = Color.LightGray
        btLog.FlatStyle = FlatStyle.Flat
        btLog.Location = New Point(121, 187)
        btLog.Name = "btLog"
        btLog.Size = New Size(80, 30)
        btLog.TabIndex = 17
        btLog.Text = "Exibir Log"
        ToolTip1.SetToolTip(btLog, "Log de mensagens do sistema")
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
        btCancelar.BackColor = Color.Firebrick
        btCancelar.Cursor = Cursors.Hand
        btCancelar.Enabled = False
        btCancelar.FlatAppearance.BorderColor = Color.DarkRed
        btCancelar.FlatAppearance.MouseOverBackColor = Color.DarkRed
        btCancelar.FlatStyle = FlatStyle.Flat
        btCancelar.ForeColor = Color.White
        btCancelar.Location = New Point(326, 187)
        btCancelar.Name = "btCancelar"
        btCancelar.Size = New Size(60, 30)
        btCancelar.TabIndex = 20
        btCancelar.Text = "Parar"
        ToolTip1.SetToolTip(btCancelar, "Para todos os downloads")
        btCancelar.UseVisualStyleBackColor = False
        ' 
        ' timerFakeProgress
        ' 
        ' 
        ' Button1
        ' 
        Button1.Cursor = Cursors.Hand
        Button1.FlatAppearance.BorderColor = Color.DarkGray
        Button1.FlatAppearance.MouseOverBackColor = Color.LightGray
        Button1.FlatStyle = FlatStyle.Flat
        Button1.Location = New Point(458, 187)
        Button1.Name = "Button1"
        Button1.Size = New Size(45, 30)
        Button1.TabIndex = 21
        Button1.Text = "..."
        ToolTip1.SetToolTip(Button1, "Abre a pasta de downloads")
        Button1.UseVisualStyleBackColor = True
        ' 
        ' Form1
        ' 
        ClientSize = New Size(516, 480)
        Controls.Add(Button1)
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
    Friend WithEvents Button1 As Button
    Friend WithEvents ToolTip1 As ToolTip
End Class
