Imports System.Diagnostics
Imports System.IO
Imports System.Security.Policy
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Public Class Form1

    Private downloadFilePath As String = Application.StartupPath & "\download.txt" ' pode ser caminho absoluto se preferir
    Private batFilePath As String = Application.StartupPath & "\run.bat" ' pode ser caminho absoluto se necessário

    Private Async Sub btnAdicionar_Click(sender As Object, e As EventArgs) Handles btnAdicionar.Click
        Dim link As String = txtUrl.Text.Trim()
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
            txtLog.AppendText($"📺 Playlist contém {total} vídeos." & Environment.NewLine)
        Catch ex As Exception
            txtLog.AppendText("❌ Falha ao contar vídeos: " & ex.Message & Environment.NewLine)
        End Try

    End Sub


    Public Async Function ExecutarProcessoAsync(ByVal logTextBox As TextBox, ByVal progressBar As ProgressBar) As Task(Of Boolean)
        Dim tcs As New TaskCompletionSource(Of Boolean)()
        Dim hasErrors As Boolean = False

        progressBar.Invoke(Sub() progressBar.Value = 0)

        Dim psi As New ProcessStartInfo()

        psi.FileName = IO.Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")

        Dim argumentos As New System.Text.StringBuilder()
        argumentos.Append("--batch-file ""download.txt"" ")
        If CheckBoxAudio.Checked Then
            argumentos.Append("--extract-audio --audio-format mp3 ")
        End If
        If chkLegendas.Checked Then
            argumentos.Append("--write-sub --sub-langs ""pt.*"" --embed-subs ")
        End If
        argumentos.Append("--output ""downloaded\%(title)s.%(ext)s"" ")
        argumentos.Append("--ignore-errors ")
        argumentos.Append("--cookies ""cookies.txt"" ")
        argumentos.Append("--format ""bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best""")


        psi.Arguments = argumentos.ToString()

        psi.WorkingDirectory = Application.StartupPath
        psi.UseShellExecute = False
        psi.RedirectStandardOutput = True
        psi.RedirectStandardError = True
        psi.CreateNoWindow = True

        Dim proc As New Process()
        proc.StartInfo = psi
        proc.EnableRaisingEvents = True

        ' Handler para a saída padrão (output)
        AddHandler proc.OutputDataReceived, Sub(s, ev)
                                                If ev.Data IsNot Nothing Then
                                                    logTextBox.Invoke(Sub() logTextBox.AppendText(ev.Data & Environment.NewLine))
                                                    If ev.Data.Contains("[download] Destination:") Then
                                                        progressBar.Invoke(Sub() progressBar.Value = 0)
                                                    End If

                                                    ' Tenta extrair o progresso da linha de saída
                                                    Dim match As Match = Regex.Match(ev.Data, "\[download\]\s+(\d{1,3}(?:\.\d+)?)%")
                                                    If match.Success Then
                                                        Dim percentText = match.Groups(1).Value.Replace(",", ".")
                                                        Dim progressVal As Integer = CInt(Math.Floor(Double.Parse(percentText, Globalization.CultureInfo.InvariantCulture)))
                                                        progressVal = Math.Min(progressVal, 100)
                                                        progressBar.Invoke(Sub()
                                                                               If progressVal <= progressBar.Maximum Then
                                                                                   progressBar.Value = progressVal
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

    Private Async Function ValidarLink(url As String) As Task(Of Boolean)
        Try

            Dim psi As New ProcessStartInfo With {
        .FileName = "app\yt-dlp.exe",
        .Arguments = $"--simulate {url}",
        .UseShellExecute = False,
        .RedirectStandardError = True,
        .RedirectStandardOutput = True,
        .CreateNoWindow = True
    }

        Dim output As String = ""
        Using proc As Process = Process.Start(psi)
            output = Await proc.StandardError.ReadToEndAsync()
            proc.WaitForExit()
        End Using

            Return Not output.Contains("ERROR:")

        Catch ex As Exception
            txtLog.AppendText(Environment.NewLine & ex.Message & Environment.NewLine)
        End Try
    End Function

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
    End Sub

    Private Sub btLimparLista_Click(sender As Object, e As EventArgs) Handles btLimparLista.Click
        LimparArquivoDownload()
    End Sub
    Private Async Sub btnExecutar_Click(sender As Object, e As EventArgs) Handles btnExecutar.Click
        txtLog.Clear()
        btnExecutar.Enabled = False
        progressBarDownload.Value = 0 ' Garante que a barra de progresso esteja zerada ao iniciar

        Try
            Dim sucesso As Boolean = Await ExecutarProcessoAsync(txtLog, progressBarDownload)

            If sucesso Then
                txtLog.AppendText(Environment.NewLine & "✅ Arquivos baixados com sucesso!" & Environment.NewLine)
                Dim pastaDestino As String = IO.Path.Combine(Application.StartupPath, "downloaded")
                If IO.Directory.Exists(pastaDestino) Then
                    Process.Start("explorer.exe", pastaDestino)
                Else
                    txtLog.AppendText("⚠️ Pasta de destino 'downloaded' não encontrada." & Environment.NewLine)
                End If
            Else
                txtLog.AppendText(Environment.NewLine & "❌ O processo foi concluído com erros." & Environment.NewLine)
            End If

        Catch ex As Exception
            txtLog.AppendText(Environment.NewLine & $"[ERRO INESPERADO] {ex.Message}")
        Finally
            btnExecutar.Enabled = True
            progressBarDownload.Value = 0 ' Reseta a barra de progresso ao finalizar
        End Try
    End Sub
    Private Sub btLog_Click(sender As Object, e As EventArgs) Handles btLog.Click
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

End Class
