Imports System.Diagnostics
Imports System.IO
Imports System.Security.Policy
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Public Class Form1
    Private downloadFilePath As String = Application.StartupPath & "\download.txt"
    Private batFilePath As String = Application.StartupPath & "\run.bat"
    Private totalLinks As Integer = 0
    Private linksConcluidos As Integer = 0
    Private processoYtDlp As Process = Nothing

    Private Async Sub BtnAdicionar_Click(sender As Object, e As EventArgs) Handles btnAdicionar.Click
        StatusLabel.Text = "Status: Adicionando link..."
        Application.DoEvents()

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
            StatusLabel.Text = $"Status: {total} vídeos encontrados"
        Catch ex As Exception
            StatusLabel.Text = "Status: Nenhum video encontrado."
            txtLog.AppendText("❌ Falha ao contar vídeos: " & ex.Message & Environment.NewLine)
        End Try
        Me.Cursor = Cursors.Default

    End Sub

    Private Sub AtualizarStatus(texto As String)
        If StatusLabel.GetCurrentParent.InvokeRequired Then
            StatusLabel.GetCurrentParent.Invoke(Sub()
                                                    StatusLabel.Text = texto
                                                End Sub)
        Else
            StatusLabel.Text = texto
        End If
    End Sub


    Private Sub LimparArquivoDownload()
        Dim caminho As String = Path.Combine(Application.StartupPath, "download.txt")
        Try
            File.WriteAllText(caminho, String.Empty)
            txtLog.AppendText(Environment.NewLine & "🧹 Arquivo download.txt limpo com sucesso!" & Environment.NewLine)
            lstLinks.Items.Clear() ' Se estiver usando ListBox para mostrar os links
            StatusLabel.Text = "Status: OK"
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

        If count = 0 Then
            ' Se não retornou nada com --flat-playlist, é um vídeo único
            Return 1
        Else
            Return count
        End If

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

    Private Function IsHLS(link As String) As Boolean
        Try
            Dim ytDlpPath As String = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")

            Dim psi As New ProcessStartInfo() With {
            .FileName = ytDlpPath,
            .Arguments = $"--dump-json --no-warnings {link}",
            .UseShellExecute = False,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True
        }

            Using proc As Process = Process.Start(psi)
                Dim output As String = proc.StandardOutput.ReadToEnd()
                proc.WaitForExit()

                If output.Contains("""is_live"": true") Then
                    Return True
                End If

                If output.Contains("""live_status"": ""is_live""") Then
                    Return True
                End If
            End Using
        Catch ex As Exception
            txtLog.Invoke(Sub() txtLog.AppendText($"[ERRO ao detectar protocolo HLS] {ex.Message}" & Environment.NewLine))
        End Try

        Return False
    End Function


    Private Async Sub BtnExecutar_Click(sender As Object, e As EventArgs) Handles btnExecutar.Click
        txtLog.Clear()

        progressBarDownload.Value = 0
        linksConcluidos = 0

        If File.Exists(downloadFilePath) Then
            totalLinks = File.ReadAllLines(downloadFilePath).Count(Function(l) Not String.IsNullOrWhiteSpace(l))
        End If

        If totalLinks = 0 Then
            MessageBox.Show("Nenhum link válido encontrado.")
            txtLog.AppendText(Environment.NewLine & "❌ Nenhum link válido encontrado." & Environment.NewLine)
            Exit Sub
        End If

        progressBarDownload.Maximum = totalLinks * 100
        timerFakeProgress.Start()
        btnExecutar.Enabled = False

        Dim links = File.ReadAllLines(downloadFilePath).Where(Function(l) Not String.IsNullOrWhiteSpace(l)).ToList()


        Try
            btCancelar.Enabled = True
            Dim argsVideoStream As New StringBuilder()
            Dim argsVideo As New StringBuilder()
            Dim argsAudio As New StringBuilder()

            Dim sucessoVideoStream As Boolean
            Dim sucessoVideo As Boolean
            Dim sucessoAudio As Boolean

            For Each link In links
                If IsHLS(link) Then
                    ' MsgBox("O link é de uma transmissão ao vivo ou HLS. O download pode demorar mais tempo.", MsgBoxStyle.Information, "Transmissão ao Vivo ou HLS Detectado")
                    argsVideoStream.Append($"--no-batch-file ""{link}"" ")
                    argsVideoStream.Append("--format best ")
                    argsVideoStream.Append($"--output ""{My.Settings.destFolder}\%(title)s_video.%(ext)s"" ")
                    argsVideoStream.Append("--ignore-errors ")
                    argsVideoStream.Append("--cookies ""cookies.txt"" ")
                    argsVideoStream.Append("--no-warnings ")
                    sucessoVideoStream = Await ExecutarProcessoAsync(txtLog, progressBarDownload, argsVideoStream.ToString())
                Else
                    '  MsgBox("O link é de um vídeo normal. O download será dividido em vídeo e áudio.", MsgBoxStyle.Information, "Vídeo Normal Detectado")
                    argsVideo.Append($"--batch-file ""download.txt"" ")
                    argsVideo.Append("--format bestvideo/best ")
                    argsVideo.Append($"--output ""{My.Settings.destFolder}\%(title)s_video.%(ext)s"" ")
                    argsVideo.Append("--ignore-errors ")
                    argsVideo.Append("--cookies ""cookies.txt"" ")
                    argsVideo.Append("--no-warnings ")
                    sucessoVideo = Await ExecutarProcessoAsync(txtLog, progressBarDownload, argsVideo.ToString())

                    argsAudio.Append($"--batch-file ""download.txt"" ")
                    argsAudio.Append("--format bestaudio/best ")
                    argsAudio.Append($"--output ""{My.Settings.destFolder}\%(title)s_audio.%(ext)s"" ")
                    argsAudio.Append("--ignore-errors ")
                    argsAudio.Append("--cookies ""cookies.txt"" ")
                    argsAudio.Append("--no-warnings ")
                    sucessoAudio = Await ExecutarProcessoAsync(txtLog, progressBarDownload, argsAudio.ToString())
                End If

            Next


            ' Dim sucesso As Boolean = Await ExecutarProcessoAsync(txtLog, progressBarDownload)

            If (sucessoVideo And sucessoAudio) Or sucessoVideoStream Then
                MergeTodosOsVideosEAudios()
                txtLog.AppendText(Environment.NewLine & "✅ Arquivos baixados com sucesso!" & Environment.NewLine)
                OpenFolder()
                StatusLabel.Text = "Status: OK"
                Application.DoEvents()
            Else
                txtLog.AppendText(Environment.NewLine & "❌ O processo foi encerrado com erros." & Environment.NewLine)
                StatusLabel.Text = "Status: Falha no download"
                Application.DoEvents()
            End If

        Catch ex As Exception
            txtLog.AppendText(Environment.NewLine & $"[ERRO INESPERADO] {ex.Message}")
            StatusLabel.Text = "Status: Falha no download..."
            Application.DoEvents()
        Finally
            btnExecutar.Enabled = True
            btCancelar.Enabled = False
            progressBarDownload.Value = 0 ' Reseta a barra de progresso ao finalizar
            timerFakeProgress.Stop()
            Me.Invoke(Sub()
                          Me.Cursor = Cursors.Default
                          txtLog.Cursor = Cursors.Default
                      End Sub)
        End Try
    End Sub

    Public Async Function ExecutarProcessoAsync(ByVal logTextBox As TextBox, ByVal progressBar As ProgressBar, ByVal argumentos As String) As Task(Of Boolean)

        StatusLabel.Text = "Status: Preparando..."
        Application.DoEvents()

        Dim tcs As New TaskCompletionSource(Of Boolean)()
        Dim hasErrors As Boolean = False
        Dim exitCode As Integer = -1


        progressBar.Invoke(Sub() progressBar.Value = 0)

        Dim psi As New ProcessStartInfo With {
            .FileName = IO.Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
        }

        'Dim argumentos As New System.Text.StringBuilder()
        'argumentos.Append("--batch-file ""download.txt"" ")
        'argumentos.Append("--no-warnings ")

        If CheckBoxAudio.Checked Then
            argumentos.Append("--extract-audio --audio-format mp3 ")
        End If
        If chkLegendas.Checked Then
            argumentos.Append("--write-sub --sub-langs ""pt.*"" --embed-subs ")
        End If
        ' argumentos.Append($"--output ""{My.Settings.destFolder}\%(title)s.%(ext)s"" ")
        ' argumentos.Append("--ignore-errors ")
        ' argumentos.Append("--cookies ""cookies.txt"" ")
        ' argumentos.Append("--format ""bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best""")
        ' argumentos.Append("--format bestvideo ")
        ' argumentos.Append($"--output ""{My.Settings.destFolder}\%(title)s_video.%(ext)s"" ")
        ' argumentos.Append("--format bestaudio ")
        ' argumentos.Append($"--output ""{My.Settings.destFolder}\%(title)s_audio.%(ext)s"" ")



        psi.Arguments = argumentos

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
                                                    If ev.Data.Contains("Deleting original file") Then
                                                        ' Ignora completamente essa linha
                                                        Return
                                                    End If

                                                    logTextBox.Invoke(Sub() logTextBox.AppendText(ev.Data & Environment.NewLine))
                                                    If ev.Data.Contains("[download] Destination:") Then
                                                        progressBar.Invoke(Sub() progressBar.Value = 0)
                                                    End If

                                                    If ev.Data.Contains("[Merging formats]") OrElse ev.Data.Contains("[Merger]") Then
                                                        linksConcluidos += 1
                                                        AtualizarStatus("Status: Finalizando aguarde...")
                                                        Me.Invoke(Sub() Me.Cursor = Cursors.WaitCursor)
                                                        txtLog.Invoke(Sub() txtLog.Cursor = Cursors.WaitCursor)
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
                                                        AtualizarStatus("Status: Download em andamento...")
                                                    End If
                                                End If
                                            End Sub


        ' Handler para a saída de erro (error)
        AddHandler proc.ErrorDataReceived, Sub(s, ev)
                                               'If ev.Data IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(ev.Data) Then
                                               '    hasErrors = True
                                               '    logTextBox.Invoke(Sub() logTextBox.AppendText("[ERRO] " & ev.Data & Environment.NewLine))
                                               'End If

                                               If ev.Data IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(ev.Data) Then
                                                   Dim ignorar As Boolean = False

                                                   Dim linhasIgnoradas = New String() {
                                                        "Skip ('#EXT-",
                                                        "Opening 'http",
                                                        "size=",
                                                        "frame=",
                                                        "bitrate=",
                                                        "speed=",
                                                        "[hls @",
                                                        "[https @",
                                                        "[tcp @",
                                                        "[tls @",
                                                        "[NULL @",
                                                        "[AVIOContext @",
                                                        "[Parsed_",
                                                        "[mp4 @",
                                                        "[matroska @",
                                                        "[mov @",
                                                        "[mpegts @",
                                                        "Input #",
                                                        "Duration:",
                                                        "Program ",
                                                        "Stream #",
                                                        "Metadata:",
                                                        "Output #",
                                                        "Stream mapping:",
                                                        "Press [q] to stop",
                                                        "encoder"
                                                   }

                                                   For Each prefixo In linhasIgnoradas
                                                       If ev.Data.StartsWith(prefixo) Then
                                                           ignorar = True
                                                           Exit For
                                                       End If
                                                   Next

                                                   If Not ignorar Then
                                                       Me.Invoke(Sub() txtLog.AppendText("[ERRO] " & ev.Data & Environment.NewLine))
                                                   End If
                                               End If
                                           End Sub

        ' Handler para quando o processo for finalizado
        AddHandler proc.Exited, Sub(s, ev)
                                    Try
                                        exitCode = proc.ExitCode
                                    Catch ex As Exception
                                        exitCode = -1 ' Em caso de erro para pegar o ExitCode
                                    End Try
                                    tcs.TrySetResult(True)
                                End Sub

        Try
            proc.Start()
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()
        Catch ex As Exception
            logTextBox.AppendText("[FALHA CRÍTICA] Não foi possível iniciar o processo: " & ex.Message)
            tcs.TrySetResult(False)
            StatusLabel.Text = "Status: Falha no processo..."
            Application.DoEvents()
        End Try

        Await tcs.Task

        ' Combinação de ExitCode + análise de stderr
        Dim sucessoFinal As Boolean = (exitCode = 0 AndAlso Not hasErrors)

        Return sucessoFinal

    End Function

    Private Sub MergeTodosOsVideosEAudios()
        Dim pastaDestino As String = Path.Combine(Application.StartupPath, My.Settings.destFolder)
        Dim arquivosVideo = Directory.GetFiles(pastaDestino, "*_video.*", SearchOption.TopDirectoryOnly)
        Dim arquivosAudio = Directory.GetFiles(pastaDestino, "*_audio.*", SearchOption.TopDirectoryOnly)

        For Each video In arquivosVideo
            Dim nomeBase = Path.GetFileNameWithoutExtension(video).Replace("_video", "")
            Dim audio = arquivosAudio.FirstOrDefault(Function(a) Path.GetFileNameWithoutExtension(a).Replace("_audio", "") = nomeBase)

            If Not String.IsNullOrEmpty(audio) Then
                Dim outputFinal = Path.Combine(pastaDestino, nomeBase & ".mp4")
                ExecutarMergeSeguro(video, audio, outputFinal)

                ' Apaga os arquivos individuais
                Try
                    File.Delete(video)
                    File.Delete(audio)
                    ' Me.Invoke(Sub() txtLog.AppendText($"🗑️ Removidos: {Path.GetFileName(video)} e {Path.GetFileName(audio)}" & Environment.NewLine))
                Catch ex As Exception
                    Me.Invoke(Sub() txtLog.AppendText($"[ERRO ao excluir arquivos fonte] {ex.Message}" & Environment.NewLine))
                End Try
            End If
        Next
    End Sub

    Private Sub ExecutarMergeSeguro(video As String, audio As String, outputFinal As String)
        Try
            Dim ffmpegPath As String = Path.Combine(Application.StartupPath, "app", "ffmpeg.exe")
            Dim psi As New ProcessStartInfo With {
            .FileName = ffmpegPath,
            .Arguments = $"-y -i ""{video}"" -i ""{audio}"" -c copy -movflags +faststart ""{outputFinal}""",
            .UseShellExecute = False,
            .CreateNoWindow = True,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True
        }

            Using ffmpegProc As Process = Process.Start(psi)
                ' Descartar stdout e stderr pra evitar travamento de buffer
                AddHandler ffmpegProc.OutputDataReceived, Sub(sender, e)
                                                          End Sub
                AddHandler ffmpegProc.ErrorDataReceived, Sub(sender, e)
                                                         End Sub

                ffmpegProc.BeginOutputReadLine()
                ffmpegProc.BeginErrorReadLine()

                ' Timeout de segurança: 30 segundos
                If Not ffmpegProc.WaitForExit(30000) Then
                    Try
                        ffmpegProc.Kill()
                        Me.Invoke(Sub() txtLog.AppendText($"[ERRO] ffmpeg travado ao tentar unir: {Path.GetFileName(outputFinal)}. Timeout forçado." & Environment.NewLine))
                    Catch
                    End Try
                Else
                    '  Me.Invoke(Sub() txtLog.AppendText($"✅ Merge concluído: {Path.GetFileName(outputFinal)}" & Environment.NewLine))
                End If
            End Using

        Catch ex As Exception
            Me.Invoke(Sub() txtLog.AppendText($"[ERRO no merge de {Path.GetFileName(outputFinal)}] {ex.Message}" & Environment.NewLine))
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
        Dim pastaDestino = Path.Combine(Application.StartupPath, My.Settings.destFolder)

        Task.Run(Sub()
                     If processoYtDlp IsNot Nothing Then
                         Try
                             If Not processoYtDlp.HasExited Then
                                 processoYtDlp.Kill(entireProcessTree:=True)
                                 processoYtDlp.WaitForExit()
                             End If

                             Dim arquivosPart = Directory.GetFiles(pastaDestino, "*.part", SearchOption.TopDirectoryOnly)
                             Dim arquivosVideo = arquivosPart.Where(Function(f) f.EndsWith(".mp4.part") OrElse f.EndsWith(".webm.part")).ToList()
                             Dim arquivosAudio = arquivosPart.Where(Function(f) f.EndsWith(".m4a.part") OrElse (f.EndsWith(".webm.part") AndAlso Not f.EndsWith(".mp4.part"))).ToList()

                             ' 1. Renomear .part para o nome final
                             For Each arquivo In arquivosPart
                                 Dim novoNome = Path.Combine(pastaDestino, Path.GetFileNameWithoutExtension(arquivo))
                                 Try
                                     File.Move(arquivo, novoNome)
                                 Catch ex As Exception
                                     Me.Invoke(Sub() txtLog.AppendText($"[ERRO ao renomear {Path.GetFileName(arquivo)}] {ex.Message}" & Environment.NewLine))
                                 End Try
                             Next

                             ' 2. Atualizar listas agora sem ".part"
                             arquivosVideo = Directory.GetFiles(pastaDestino, "*.mp4", SearchOption.TopDirectoryOnly).ToList()
                             arquivosAudio = Directory.GetFiles(pastaDestino, "*.m4a", SearchOption.TopDirectoryOnly).ToList()


                             For Each video In arquivosVideo
                                 Dim nomeBase = Path.GetFileNameWithoutExtension(video).Replace(".mp4", "").Replace(".webm", "")
                                 Dim audio = arquivosAudio.FirstOrDefault(Function(a) Path.GetFileNameWithoutExtension(a).Contains(nomeBase))

                                 If Not String.IsNullOrEmpty(audio) Then
                                     Dim outputFinal = Path.Combine(pastaDestino, nomeBase & "_merged.mp4")
                                     Dim ffmpegPath = Path.Combine(Application.StartupPath, "app", "ffmpeg.exe")
                                     Dim psi As New ProcessStartInfo(ffmpegPath, $"-y -i ""{video}"" -i ""{audio}"" -c copy ""{outputFinal}""") With {
                                     .CreateNoWindow = True,
                                     .UseShellExecute = False
                                 }

                                     Using ffmpegProc As Process = Process.Start(psi)
                                         ffmpegProc.WaitForExit()
                                     End Using

                                     Try
                                         File.Delete(video)
                                         File.Delete(audio)
                                     Catch ex As Exception
                                         Me.Invoke(Sub() txtLog.AppendText($"[ERRO ao excluir .part] {ex.Message}" & Environment.NewLine))
                                     End Try

                                     Me.Invoke(Sub() txtLog.AppendText($"🎬 Merge finalizado: {Path.GetFileName(outputFinal)}" & Environment.NewLine))
                                 End If
                             Next

                             Me.Invoke(Sub()
                                           progressBarDownload.Value = 0
                                           txtLog.AppendText(Environment.NewLine & "⛔ Download interrompido pelo usuário." & Environment.NewLine)
                                           btCancelar.Enabled = False
                                           btnExecutar.Enabled = True
                                           StatusLabel.Text = "Status: Cancelado pelo usuário."
                                           timerFakeProgress.Stop()
                                       End Sub)

                             If Directory.Exists(pastaDestino) AndAlso Directory.EnumerateFiles(pastaDestino).Any() Then
                                 Process.Start("explorer.exe", pastaDestino)
                             Else
                                 Me.Invoke(Sub() txtLog.AppendText("⚠️ Nenhum arquivo parcial encontrado." & Environment.NewLine))
                             End If

                         Catch ex As Exception
                             Me.Invoke(Sub()
                                           txtLog.AppendText($"[ERRO ao parar o processo] {ex.Message}" & Environment.NewLine)
                                           btCancelar.Enabled = False
                                           btnExecutar.Enabled = True
                                           timerFakeProgress.Stop()
                                       End Sub)
                         End Try
                     Else
                         Me.Invoke(Sub()
                                       txtLog.AppendText("⚠️ Nenhum processo ativo para interromper." & Environment.NewLine)
                                       btCancelar.Enabled = False
                                       btnExecutar.Enabled = True
                                       timerFakeProgress.Stop()
                                   End Sub)
                     End If
                 End Sub)
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
