Imports System.Diagnostics
Imports System.IO
Imports System.Security.Policy
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Public Class Form1
    Private downloadFilePath As String = Application.StartupPath & "\download.txt"
    Private batFilePath As String = Application.StartupPath & "\run.bat"
    Private totalLinks As Integer = 0
    Private linksConcluidos As Integer = 0
    Private processoYtDlp As Process = Nothing

    Private Async Sub BtnAdicionar_Click(sender As Object, e As EventArgs) Handles btnAdicionar.Click
        Dim link As String = txtUrl.Text.Trim()
        Me.Cursor = Cursors.WaitCursor
        If link <> "" Then
            File.AppendAllText(downloadFilePath, link & Environment.NewLine)
            lstLinks.Items.Add(link)
            txtUrl.Clear()
        End If

        If Not File.Exists(downloadFilePath) Then
            MessageBox.Show("download.txt não encontrado.")
            Return
        End If

        Dim links = File.ReadAllLines(downloadFilePath).Where(Function(l) Not String.IsNullOrWhiteSpace(l)).ToList()
        If links.Count = 0 Then
            txtLog.AppendText("⚠️ Nenhum link encontrado no arquivo." & Environment.NewLine)
            Return
        End If

        Try
            Dim total As Integer = Await ContarVideosNaPlaylist(link)
            txtLog.AppendText($"📺 Link contém {total} vídeos." & Environment.NewLine)
        Catch ex As Exception
            txtLog.AppendText("❌ Falha ao contar vídeos: " & ex.Message & Environment.NewLine)
        End Try
        Me.Cursor = Cursors.Default
    End Sub

    Public Async Function ExecutarProcessoAsync(ByVal logTextBox As TextBox, ByVal progressBar As ProgressBar) As Task(Of Boolean)
        Dim tcs As New TaskCompletionSource(Of Boolean)()
        Dim hasErrors As Boolean = False

        progressBar.Invoke(Sub() progressBar.Value = 0)

        Dim psi As New ProcessStartInfo With {
            .FileName = IO.Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
        }

        Dim argumentos As New System.Text.StringBuilder()
        argumentos.Append("--batch-file ""download.txt"" ")
        argumentos.Append("--no-warnings ")
        If CheckBoxAudio.Checked Then
            argumentos.Append("--extract-audio --audio-format mp3 ")
        End If
        If chkLegendas.Checked Then
            argumentos.Append("--write-sub --sub-langs ""pt.*"" --embed-subs ")
        End If
        argumentos.Append($"--output ""{My.Settings.destFolder}\%(title)s.%(ext)s"" ")
        argumentos.Append("--ignore-errors ")
        argumentos.Append("--cookies ""cookies.txt"" ")
        argumentos.Append("--format ""bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best""")


        psi.Arguments = argumentos.ToString()

        psi.WorkingDirectory = Application.StartupPath
        psi.UseShellExecute = False
        psi.RedirectStandardOutput = True
        psi.RedirectStandardError = True
        psi.CreateNoWindow = True

        processoYtDlp = New Process()
        Dim proc = processoYtDlp
        proc.StartInfo = psi
        proc.EnableRaisingEvents = True

        ' Handler para a saída padrão (output)
        AddHandler proc.OutputDataReceived, Sub(s, ev)
                                                If ev.Data IsNot Nothing Then
                                                    logTextBox.Invoke(Sub() logTextBox.AppendText(ev.Data & Environment.NewLine))
                                                    If ev.Data.Contains("[download] Destination:") Then
                                                        progressBar.Invoke(Sub() progressBar.Value = 0)
                                                    End If

                                                    If ev.Data.Contains("[download] 100%") Then
                                                        linksConcluidos += 1
                                                    End If

                                                    ' Tenta extrair o progresso da linha de saída
                                                    Dim match As Match = Regex.Match(ev.Data, "\[download\]\s+(\d{1,3}(?:\.\d+)?)%")
                                                    If match.Success Then
                                                        timerFakeProgress.Stop()
                                                        Dim percentText = match.Groups(1).Value.Replace(",", ".")
                                                        Dim progressVal As Integer = CInt(Math.Floor(Double.Parse(percentText, Globalization.CultureInfo.InvariantCulture)))
                                                        progressVal = Math.Min(progressVal, 100)

                                                        Dim progressoGlobal = (linksConcluidos * 100) + progressVal

                                                        progressBar.Invoke(Sub()
                                                                               If progressoGlobal <= progressBar.Maximum Then
                                                                                   progressBar.Value = progressoGlobal
                                                                               End If
                                                                           End Sub)
                                                    End If
                                                End If
                                            End Sub

        ' Handler para a saída de erro (error)
        AddHandler proc.ErrorDataReceived, Sub(s, ev)
                                               If ev.Data IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(ev.Data) Then
                                                   hasErrors = True
                                                   logTextBox.Invoke(Sub() logTextBox.AppendText("[ERRO] " & ev.Data & Environment.NewLine))
                                               End If
                                           End Sub

        ' Handler para quando o processo for finalizado
        AddHandler proc.Exited, Sub(s, ev)
                                    tcs.TrySetResult(Not hasErrors)
                                End Sub

        Try
            proc.Start()
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()
        Catch ex As Exception
            logTextBox.AppendText("[FALHA CRÍTICA] Não foi possível iniciar o processo: " & ex.Message)
            tcs.TrySetResult(False)
        End Try

        Return Await tcs.Task
    End Function
    Private Sub LimparArquivoDownload()
        Dim caminho As String = Path.Combine(Application.StartupPath, "download.txt")
        Try
            File.WriteAllText(caminho, String.Empty)
            txtLog.AppendText(Environment.NewLine & "🧹 Arquivo download.txt limpo com sucesso!" & Environment.NewLine)
            lstLinks.Items.Clear() ' Se estiver usando ListBox para mostrar os links
        Catch ex As Exception
            MessageBox.Show("Erro ao limpar o arquivo: " & ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Private Async Function ContarVideosNaPlaylist(url As String) As Task(Of Integer)
        Dim psi As New ProcessStartInfo With {
        .FileName = "app\yt-dlp.exe",
        .Arguments = $"--flat-playlist --print ""%(title)s"" {url}",
        .UseShellExecute = False,
        .RedirectStandardOutput = True,
        .CreateNoWindow = True
    }

        Dim count As Integer = 0
        Using proc As Process = Process.Start(psi)
            While Not proc.StandardOutput.EndOfStream
                Await proc.StandardOutput.ReadLineAsync()
                count += 1
            End While
            proc.WaitForExit()
        End Using
        Return count
    End Function

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If File.Exists(downloadFilePath) Then
            Dim links = File.ReadAllLines(downloadFilePath)
            lstLinks.Items.AddRange(links)
        End If
        progressBarDownload.Location = New Point(12, 222)
        Me.Height = 315
        AddHandler timerFakeProgress.Tick, AddressOf timerFakeProgress_Tick

    End Sub

    Private Sub BtLimparLista_Click(sender As Object, e As EventArgs) Handles btLimparLista.Click
        LimparArquivoDownload()
    End Sub
    Private Async Sub BtnExecutar_Click(sender As Object, e As EventArgs) Handles btnExecutar.Click
        txtLog.Clear()
        btnExecutar.Enabled = False
        progressBarDownload.Maximum = totalLinks * 100
        progressBarDownload.Value = 0
        linksConcluidos = 0
        timerFakeProgress.Start()

        If File.Exists(downloadFilePath) Then
            totalLinks = File.ReadAllLines(downloadFilePath).Count(Function(l) Not String.IsNullOrWhiteSpace(l))
        End If

        Try
            btCancelar.Enabled = True

            Dim sucesso As Boolean = Await ExecutarProcessoAsync(txtLog, progressBarDownload)

            If sucesso Then
                txtLog.AppendText(Environment.NewLine & "✅ Arquivos baixados com sucesso!" & Environment.NewLine)
                OpenFolder()
            Else
                txtLog.AppendText(Environment.NewLine & "❌ O processo foi concluído com erros." & Environment.NewLine)
            End If

        Catch ex As Exception
            txtLog.AppendText(Environment.NewLine & $"[ERRO INESPERADO] {ex.Message}")
        Finally
            btnExecutar.Enabled = True
            btCancelar.Enabled = False
            progressBarDownload.Value = 0 ' Reseta a barra de progresso ao finalizar
        End Try
    End Sub

    Private Sub OpenFolder()
        Dim pastaDestino As String = IO.Path.Combine(Application.StartupPath, My.Settings.destFolder)
        ' Abrir a pasta se ela existir e conter arquivos
        If IO.Directory.Exists(pastaDestino) AndAlso IO.Directory.EnumerateFiles(pastaDestino).Any() Then
            Process.Start("explorer.exe", pastaDestino)
        Else
            txtLog.AppendText("⚠️ Nenhum arquivo encontrado na pasta de destino." & Environment.NewLine)
        End If
    End Sub

    Private Sub BtLog_Click(sender As Object, e As EventArgs) Handles btLog.Click
        If txtLog.Visible Then
            btLog.Text = "Exibir Log"
            txtLog.Visible = False
            progressBarDownload.Location = New Point(12, 222)
            Me.Height = 315
        Else
            txtLog.Visible = True
            txtLog.Location = New Point(12, 222)
            progressBarDownload.Location = New Point(12, 404)
            Me.Height = 500
            btLog.Text = "Ocultar Log"
        End If

    End Sub
    Private Sub AlterarPastaDestinoToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AlterarPastaDestinoToolStripMenuItem.Click
        Dim folderBrowser As New FolderBrowserDialog With {
            .Description = "Selecione a pasta de destino para os downloads:"
        }
        If folderBrowser.ShowDialog() = DialogResult.OK Then
            My.Settings.destFolder = folderBrowser.SelectedPath
            My.Settings.Save()
            txtLog.AppendText($"🗂️ Pasta de destino alterada para: {My.Settings.destFolder}" & Environment.NewLine)
        End If

    End Sub
    Private Sub btCancelar_Click(sender As Object, e As EventArgs) Handles btCancelar.Click
        timerFakeProgress.Stop()

        If processoYtDlp IsNot Nothing Then
            Try
                If Not processoYtDlp.HasExited Then
                    processoYtDlp.Kill()
                    processoYtDlp.WaitForExit()
                    txtLog.AppendText(Environment.NewLine & "⛔ Download interrompido pelo usuário." & Environment.NewLine)
                Else
                    txtLog.AppendText(Environment.NewLine & "⚠️ O processo já havia sido finalizado." & Environment.NewLine)
                End If

            Catch ex As Exception
                txtLog.AppendText(Environment.NewLine & $"[ERRO ao parar o processo] {ex.Message}" & Environment.NewLine)
            End Try
        Else
            txtLog.AppendText(Environment.NewLine & "⚠️ Nenhum processo ativo para interromper." & Environment.NewLine)
        End If
    End Sub
    Private Sub ImportarCookiesPrivadosToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ImportarCookiesPrivadosToolStripMenuItem.Click
        Dim saveFileDialog As New OpenFileDialog With {
            .Filter = "Arquivo de Cookies (*.txt)|*.txt",
            .Title = "Importar cookies privados",
            .InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            .FileName = "cookies.txt"
        }
        If saveFileDialog.ShowDialog() = DialogResult.OK Then
            Dim cookiesPath As String = saveFileDialog.FileName
            If Not String.IsNullOrEmpty(cookiesPath) Then
                If File.Exists(cookiesPath) Then
                    File.Delete(Path.Combine(Application.StartupPath, "cookies.txt")) ' Remove o arquivo antigo, se existir
                End If
                FileCopy(cookiesPath, Path.Combine(Application.StartupPath, "cookies.txt"))
                MsgBox("Cookies privados importados com sucesso!", MsgBoxStyle.Information, "Importação de Cookies")
                txtLog.AppendText($"🍪 Cookies privados importados com sucesso: {cookiesPath}" & Environment.NewLine)
            End If
        End If
    End Sub
    Private Sub timerFakeProgress_Tick(sender As Object, e As EventArgs) Handles timerFakeProgress.Tick
        If progressBarDownload.Value < progressBarDownload.Maximum Then
            progressBarDownload.Value += 1
        Else
            progressBarDownload.Value = 0
        End If
    End Sub
End Class
