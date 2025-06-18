Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Security.Policy
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks

Public Class Form1
    Private downloadFilePath As String = Application.StartupPath & "\download.txt"
    Private pastaDestino As String = IO.Path.Combine(Application.StartupPath, My.Settings.destFolder)
    Private batFilePath As String = Application.StartupPath & "\run.bat"
    Private totalLinks As Integer = 0
    Private linksConcluidos As Integer = 0
    Private processoYtDlp As Process = Nothing
    Private ultimaLinhaHLS As String = ""
    Private ultimaLinhaPlaylist As String = ""
    Private inicioHLS As DateTime
    Private progressoAtualLink As Integer = 0
    Private canceladoPeloUsuario As Boolean = False

    Private Async Sub BtnAdicionar_Click(sender As Object, e As EventArgs) Handles btnAdicionar.Click
        Dim link As String = txtUrl.Text.Trim()

        If link <> "" Then
            If Not File.Exists(downloadFilePath) Then
                MessageBox.Show("arquivo não encontrado.")
                Return
            End If
            StatusLabel.Text = "Status: Adicionando link..."
            Application.DoEvents()
            Me.Cursor = Cursors.WaitCursor
            File.AppendAllText(downloadFilePath, link & Environment.NewLine)
            txtUrl.Clear()
        End If

        Dim links = File.ReadAllLines(downloadFilePath).Where(Function(l) Not String.IsNullOrWhiteSpace(l)).ToList()
        If links.Count = 0 Then
            txtLog.AppendText("⚠️ Nenhum link encontrado no arquivo." & Environment.NewLine)
            Return
        End If

        Try
            Dim videoData = Await ContarVideosNaPlaylist(link)
            AdicionarTituloNaListView(UnescapeUnicode(videoData.Item2), link)
            ' lstLink.Items.Add(videoData.Item2)
            txtLog.AppendText($"📺 Link contém {videoData.Item1} vídeos." & Environment.NewLine)
            StatusLabel.Text = $"Status: {videoData.Item1} vídeos encontrados"
        Catch ex As Exception
            StatusLabel.Text = "Status: Nenhum video encontrado."
            txtLog.AppendText("❌ Falha ao contar vídeos: " & ex.Message & Environment.NewLine)
        End Try
        Me.Cursor = Cursors.Default

    End Sub
    Private Sub MarcarItemComoOK(linkOriginal As String)
        For Each item As ListViewItem In lstLink.Items
            If item.Tag IsNot Nothing AndAlso item.Tag.ToString().Equals(linkOriginal, StringComparison.OrdinalIgnoreCase) Then
                item.SubItems(1).Text = "OK"
                Exit For
            End If
        Next
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
            txtLog.AppendText(Environment.NewLine & "🧹 Arquivo limpo com sucesso!" & Environment.NewLine)
            lstLink.Items.Clear() ' Se estiver usando ListBox para mostrar os links
        Catch ex As Exception
            MessageBox.Show("Erro ao limpar o arquivo: " & ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
    Private Sub RemoverLinkEspecificoDoArquivo(linkParaRemover As String)
        Try
            Dim caminho As String = Path.Combine(Application.StartupPath, "download.txt")

            If File.Exists(caminho) Then
                ' Lê todas as linhas
                Dim linhas = File.ReadAllLines(caminho).ToList()

                ' Remove todas as ocorrências exatas do link
                Dim novasLinhas = linhas.Where(Function(l) Not l.Trim().Equals(linkParaRemover.Trim(), StringComparison.OrdinalIgnoreCase)).ToList()

                ' Salva de volta no arquivo
                File.WriteAllLines(caminho, novasLinhas)

                txtLog.AppendText($"🧹 Link removido: {linkParaRemover}" & Environment.NewLine)
            End If

        Catch ex As Exception
            txtLog.AppendText($"[ERRO ao remover link do arquivo] {ex.Message}" & Environment.NewLine)
        End Try
    End Sub

    Private Async Function ContarVideosNaPlaylist(url As String) As Task(Of (Integer, String))
        Dim ytDlpPath As String = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
        Dim psi As New ProcessStartInfo With {
        .FileName = ytDlpPath,
        .Arguments = $"--dump-json --no-warnings --cookies ""cookies.txt"" ""{url}""",
        .UseShellExecute = False,
        .RedirectStandardOutput = True,
        .RedirectStandardError = True,
        .CreateNoWindow = True
    }

        Dim jsonSaida As String = ""

        Using proc As Process = Process.Start(psi)
            jsonSaida = Await proc.StandardOutput.ReadToEndAsync()
            proc.WaitForExit()
        End Using

        Dim totalVideos As Integer = 1
        Dim titulo As String = "Título desconhecido"
        Dim origem As String = ""

        Try
            If jsonSaida.Contains("entries") Then
                ' É playlist
                Dim entradas = Regex.Matches(jsonSaida, "\{""id"":")
                totalVideos = entradas.Count
            End If

            ' Captura o título da playlist ou do vídeo
            Dim matchTitulo = Regex.Match(jsonSaida, """title"":\s*""([^""]+)""")
            If matchTitulo.Success Then
                titulo = matchTitulo.Groups(1).Value
            End If

            Dim matchSite = Regex.Match(jsonSaida, """extractor_key"":\s*""([^""]+)""")
            If matchSite.Success Then
                origem = matchSite.Groups(1).Value
            End If

        Catch ex As Exception
            txtLog.AppendText($"[ERRO JSON] {ex.Message}" & Environment.NewLine)
        End Try

        Return (Math.Max(1, totalVideos), " [" & origem & "] - " & titulo)
    End Function
    Private Function UnescapeUnicode(input As String) As String
        Return System.Text.RegularExpressions.Regex.Replace(
        input,
        "\\u(?<Value>[a-fA-F0-9]{4})",
        Function(m) ChrW(Convert.ToInt32(m.Groups("Value").Value, 16))
    )
    End Function
    Private Sub AdicionarTituloNaListView(titulo As String, Optional linkOriginal As String = "")
        Dim item As New ListViewItem(titulo)
        item.Tag = linkOriginal
        item.SubItems.Add("Em fila")
        item.SubItems.Add("🗑️")
        lstLink.Items.Add(item)
    End Sub

    Private Sub lstLink_MouseClick(sender As Object, e As MouseEventArgs) Handles lstLink.MouseClick
        Dim info As ListViewHitTestInfo = lstLink.HitTest(e.Location)

        If info.Item IsNot Nothing AndAlso info.SubItem IsNot Nothing Then
            Dim colunaClicada As Integer = info.Item.SubItems.IndexOf(info.SubItem)

            ' Supondo que a coluna 2 (índice 2) seja a coluna "Ação"
            If colunaClicada = 2 Then
                Dim titulo As String = info.Item.Text
                Dim linkOriginal As String = info.Item.Tag.ToString() ' Link escondido na coluna 1 (invisível)

                ' Confirma antes de excluir
                If MessageBox.Show($"Deseja excluir o item: {titulo} ?", "Confirmação", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                    lstLink.Items.Remove(info.Item)
                    RemoverLinkEspecificoDoArquivo(linkOriginal)
                    txtLog.AppendText($"🗑️ Item excluído: {titulo}" & Environment.NewLine)

                    ' Opcional: Se quiser deletar arquivos físicos relacionados
                    'Dim pastaDestino = Path.Combine(Application.StartupPath, My.Settings.destFolder)
                    'Dim arquivos = Directory.GetFiles(pastaDestino, $"*{SanitizeFileName(titulo)}*")

                    'For Each arq In arquivos
                    '    Try
                    '        File.Delete(arq)
                    '        txtLog.AppendText($"🗑️ Arquivo deletado: {Path.GetFileName(arq)}{Environment.NewLine}")
                    '    Catch ex As Exception
                    '        txtLog.AppendText($"[ERRO ao excluir {Path.GetFileName(arq)}] {ex.Message}{Environment.NewLine}")
                    '    End Try
                    'Next
                End If
            End If
        End If
    End Sub
    Private Function LimparPrefixoExtractor(titulo As String) As String
        If titulo.StartsWith("[") Then
            Dim fechamento = titulo.IndexOf("]")
            If fechamento > 0 AndAlso fechamento < titulo.Length - 1 Then
                Return titulo.Substring(fechamento + 1).Trim()
            End If
        End If
        Return titulo
    End Function

    Private Async Function VerificarAtualizacaoYTDLP() As Task
        AtualizarStatus("Status: Verificando atualizações...")
        Dim psi As New ProcessStartInfo With {
        .FileName = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe"),
        .Arguments = "--update",
        .UseShellExecute = False,
        .RedirectStandardOutput = True,
        .RedirectStandardError = True,
        .CreateNoWindow = True
    }
        Dim output As String
        Dim errors As String

        Using proc As Process = Process.Start(psi)
            output = Await proc.StandardOutput.ReadToEndAsync()
            errors = Await proc.StandardError.ReadToEndAsync()
            txtLog.AppendText($"[Verificação de atualização yt-dlp]{Environment.NewLine}{output}{errors}{Environment.NewLine}")
            proc.WaitForExit()
        End Using

        If output.Contains("yt-dlp is up to date") Then
            AtualizarStatus("Status: Sistema está na última versão.")
        ElseIf output.Contains("Updated yt-dlp") Then
            AtualizarStatus("Status: Sistema atualizado com sucesso!")
        ElseIf output.Contains("ERROR") Then
            AtualizarStatus("Status: Falha ao atualizar!")
        End If

        Await Task.Delay(3000)
        AtualizarStatus("Status: Pronto...")

    End Function

    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        progressBarDownload.Location = New Point(12, 224)
        Me.Height = 335
        AddHandler timerFakeProgress.Tick, AddressOf timerFakeProgress_Tick
        lstLink.Columns.Add("Título", 400)
        lstLink.Columns.Add("Status", 50)
        lstLink.Columns.Add("Action", 40)

        If File.Exists(downloadFilePath) Then
            AtualizarStatus("Status: Processando links...")
            Dim links = File.ReadAllLines(downloadFilePath)
            For Each link In links
                Dim videoData = Await ContarVideosNaPlaylist(link)
                AdicionarTituloNaListView(UnescapeUnicode(videoData.Item2), link)
            Next

            For Each item As ListViewItem In lstLink.Items
                Dim padrao As String = "\[.*?\]"
                Dim titulo As String = Regex.Replace(item.Text, padrao, "").Replace("-", "").Trim
                Dim nomeSanitizado As String = UnescapeUnicode(titulo)
                Dim extensoes = New String() {".mp4", ".mp3"}
                Dim arquivos = Directory.GetFiles(pastaDestino).
               Where(Function(f) extensoes.Any(Function(ext) f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) AndAlso f.Contains(nomeSanitizado)).
               ToArray()

                If arquivos.Length > 0 Then
                    item.SubItems(1).Text = "OK"
                Else
                    item.SubItems(1).Text = "Em fila"
                End If
            Next
            AtualizarStatus("Status: Pronto...")
        End If

    End Sub
    Private Sub BtLimparLista_Click(sender As Object, e As EventArgs) Handles btLimparLista.Click
        LimparArquivoDownload()
    End Sub
    Private Function IsHLS(link As String) As Boolean
        Try

            Dim ytDlpPath As String = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
            Dim psi As New ProcessStartInfo() With {
            .FileName = ytDlpPath,
            .Arguments = $"--dump-json --no-warnings --playlist-items 1 {link}",
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
    Private Function IsPlaylist(link As String) As Boolean
        Try
            Dim ytDlpPath As String = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
            Dim psi As New ProcessStartInfo() With {
            .FileName = ytDlpPath,
            .Arguments = $"--dump-single-json --no-warnings --playlist-items 1 {link}",
            .UseShellExecute = False,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True
        }

            Using proc As Process = Process.Start(psi)
                Dim output As String = proc.StandardOutput.ReadToEnd()
                proc.WaitForExit()

                If output.Contains("""_type"": ""playlist""") Then
                    Return True
                End If
            End Using
        Catch ex As Exception
            txtLog.Invoke(Sub() txtLog.AppendText($"[ERRO ao detectar Playlist] {ex.Message}" & Environment.NewLine))
        End Try

        Return False
    End Function
    Private Async Sub BtnExecutar_Click(sender As Object, e As EventArgs) Handles btnExecutar.Click

        Dim linksList As New List(Of String)()
        If File.Exists(downloadFilePath) Then
            linksList = File.ReadAllLines(downloadFilePath).Where(Function(l) Not String.IsNullOrWhiteSpace(l)).ToList()
        End If

        If linksList.Count = 0 Then
            MessageBox.Show("Nenhum link válido encontrado.")
            txtLog.AppendText(Environment.NewLine & "❌ Nenhum link válido encontrado." & Environment.NewLine)
            Exit Sub
        End If

        txtLog.Clear()
        StatusLabel.Text = "Status: Iniciando..."
        Application.DoEvents()
        Me.Cursor = Cursors.WaitCursor
        linksConcluidos = 0
        Dim etapaAtual As Integer = 0
        Dim etapasTotais As Integer = 0
        btnExecutar.Enabled = False

        Try
            btCancelar.Enabled = True
            canceladoPeloUsuario = False
            progressoAtualLink = 0


            ' Calcula o total de etapas
            For Each link In linksList
                If IsHLS(link) OrElse IsPlaylist(link) Then
                    etapasTotais += 1
                ElseIf CheckBoxAudio.Checked Then
                    etapasTotais += 1
                Else
                    etapasTotais += 1 ' Vídeo + Áudio
                End If
            Next

            progressBarDownload.Maximum = etapasTotais * 100
            progressBarDownload.Value = 0
            etapaAtual = 0

            For Each link In linksList
                If canceladoPeloUsuario Then Exit For

                Dim linkOriginal As String = link

                If IsHLS(link) Then
                    timerFakeProgress.Start()
                    Me.Cursor = Cursors.Default
                    chkLegendas.Enabled = False
                    CheckBoxAudio.Enabled = False
                    Dim argsVideoStream As New StringBuilder()
                    argsVideoStream.Append($" ""{link}"" ")
                    argsVideoStream.Append("--format best ")
                    argsVideoStream.Append($"--output ""{My.Settings.destFolder}\%(title)s_video.%(ext)s"" ")
                    argsVideoStream.Append("--buffer-size 1M ")
                    argsVideoStream.Append("--ignore-errors ")
                    argsVideoStream.Append("--cookies ""cookies.txt"" ")
                    'argsVideoStream.Append("--cookies-from-browser chrome ")
                    argsVideoStream.Append("--no-warnings ")
                    If canceladoPeloUsuario Then Exit For
                    If Await ExecutarProcessoAsync(txtLog, progressBarDownload, etapaAtual, etapasTotais, argsVideoStream.ToString()) Then
                        etapaAtual += 1
                        MarcarItemComoOK(linkOriginal)
                    End If

                    Continue For

                Else
                    chkLegendas.Enabled = True
                    CheckBoxAudio.Enabled = True
                    Dim argsPlaylist As New StringBuilder()

                    If IsPlaylist(link) Then
                        argsPlaylist.Append("--extractor-args ""youtubetab:skip=authcheck"" ")
                        argsPlaylist.Append("--format bestvideo[ext=mp4]+bestaudio[ext=m4a] ")
                        argsPlaylist.Append($"--output ""{My.Settings.destFolder}\%(title)s_video.%(ext)s"" ""{link}"" ")
                        argsPlaylist.Append("--ignore-errors ")
                        argsPlaylist.Append("--cookies ""cookies.txt"" ")
                        'argsPlaylist.Append("--cookies-from-browser chrome ")
                        argsPlaylist.Append("--no-warnings ")
                        If chkLegendas.Checked Then
                            argsPlaylist.Append("--write-sub --sub-langs ""pt.*"" --sub-format srt --embed-subs ")
                        End If
                        If canceladoPeloUsuario Then Exit For
                        If Await ExecutarProcessoAsync(txtLog, progressBarDownload, etapaAtual, etapasTotais, argsPlaylist.ToString()) Then
                            etapaAtual += 1
                            MarcarItemComoOK(linkOriginal)
                        End If

                        Continue For

                    End If

                    If CheckBoxAudio.Checked Then
                        Dim argsAudio As New StringBuilder()
                        argsAudio.Append("--extract-audio --audio-format mp3 ")
                        argsAudio.Append("--format bestaudio/best ")
                        argsAudio.Append($"--output ""{My.Settings.destFolder}\%(title)s.%(ext)s"" ""{link}"" ")
                        argsAudio.Append("--ignore-errors ")
                        argsAudio.Append("--cookies ""cookies.txt"" ")
                        ' argsAudio.Append("--cookies-from-browser chrome ")
                        argsAudio.Append("--no-warnings ")
                        If canceladoPeloUsuario Then Exit For
                        If Await ExecutarProcessoAsync(txtLog, progressBarDownload, etapaAtual, etapasTotais, argsAudio.ToString()) Then
                            MarcarItemComoOK(linkOriginal)
                            etapaAtual += 1
                        End If

                        Continue For

                    End If

                    Dim argsSingleVideo As New StringBuilder()
                    argsSingleVideo.Append("--extractor-args ""youtubetab:skip=authcheck"" ")
                    argsSingleVideo.Append("--format bestvideo[ext=mp4]+bestaudio[ext=m4a] --no-playlist ")
                    argsSingleVideo.Append($"--output ""{My.Settings.destFolder}\%(title)s.%(ext)s"" ""{link}"" ")
                    argsSingleVideo.Append("--ignore-errors ")
                    argsSingleVideo.Append("--cookies ""cookies.txt"" ")
                    ' argsSingleVideo.Append("--cookies-from-browser chrome ")
                    argsSingleVideo.Append("--no-warnings ")
                    If chkLegendas.Checked Then
                        argsSingleVideo.Append("--write-sub --sub-langs ""pt.*"" --sub-format srt --embed-subs ")
                    End If
                    If canceladoPeloUsuario Then Exit For
                    If Await ExecutarProcessoAsync(txtLog, progressBarDownload, etapaAtual, etapasTotais, argsSingleVideo.ToString()) Then
                        MarcarItemComoOK(linkOriginal)
                        etapaAtual += 1
                    End If

                End If

            Next

            If Not canceladoPeloUsuario Then
                ' If linkIsPlaylist Or linkIsOnlyAudio Then
                txtLog.AppendText(Environment.NewLine & "✅ Arquivos baixados com sucesso!" & Environment.NewLine)
                OpenFolder()
                StatusLabel.Text = "Status: Download concluido!"
                Application.DoEvents()
                Me.Cursor = Cursors.Default
            End If

        Catch ex As Exception
            txtLog.AppendText(Environment.NewLine & $"[ERRO INESPERADO] {ex.Message}")
            StatusLabel.Text = "Status: Falha no download..."
            Application.DoEvents()
        Finally
            canceladoPeloUsuario = False
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

    Public Async Function ExecutarProcessoAsync(ByVal logTextBox As TextBox, ByVal progressBar As ProgressBar, ByVal etapaAtual As Integer, ByVal totalEtapas As Integer, ByVal argumentos As String) As Task(Of Boolean)

        Dim tcs As New TaskCompletionSource(Of Boolean)()
        Dim hasErrors As Boolean = False
        Dim exitCode As Integer = -1


        ' progressBar.Invoke(Sub() progressBar.Value = 0)

        Dim psi As New ProcessStartInfo With {
            .FileName = IO.Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
        }

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

                                                    If ev.Data.Contains("[download] Downloading item") Then

                                                        Me.Invoke(Sub()
                                                                      Dim statusText As String = ev.Data.Replace("[download] Downloading item", "Status: Baixando item da playlist ")
                                                                      StatusLabel.Text = statusText
                                                                  End Sub)

                                                    End If

                                                    If ev.Data.Contains("[Merger] Merging formats into") Then
                                                        AtualizarStatus("Status: Finalizando aguarde...")
                                                    End If

                                                    If ev.Data.Contains("Deleting original file") Then
                                                        ' Ignora completamente essa linha
                                                        Return
                                                    End If

                                                    logTextBox.Invoke(Sub() logTextBox.AppendText(ev.Data & Environment.NewLine))
                                                    If ev.Data.Contains("[download] Destination:") Then
                                                        progressBar.Invoke(Sub() progressBar.Value = 0)
                                                    End If

                                                    ' Tenta extrair o progresso da linha de saída
                                                    Dim match As Match = Regex.Match(ev.Data, "\[download\]\s+(\d{1,3}(?:\.\d+)?)%")
                                                    If match.Success Then
                                                        timerFakeProgress.Stop()
                                                        Dim percentText = match.Groups(1).Value.Replace(",", ".")
                                                        Dim percentEtapa As Integer = CInt(Math.Floor(Double.Parse(percentText, Globalization.CultureInfo.InvariantCulture)))
                                                        percentEtapa = Math.Min(percentEtapa, 100)

                                                        Dim progressoGlobal As Integer = (etapaAtual * 100) + percentEtapa

                                                        Me.Invoke(Sub()
                                                                      progressBarDownload.Maximum = totalEtapas * 100
                                                                      progressBarDownload.Value = Math.Min(progressoGlobal, progressBarDownload.Maximum)
                                                                  End Sub)

                                                        AtualizarStatus("Status: Download em andamento...")
                                                        Me.Invoke(Sub()
                                                                      Me.Cursor = Cursors.Default
                                                                      txtLog.Cursor = Cursors.Default
                                                                  End Sub)
                                                    End If

                                                End If

                                            End Sub


        ' Handler para a saída de erro (error)
        AddHandler proc.ErrorDataReceived, Sub(s, ev)
                                               If ev.Data IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(ev.Data) Then
                                                   Dim linha = ev.Data.ToLower()

                                                   ' Termos típicos de HLS que queremos interceptar
                                                   Dim termosHLS = New String() {
                                                       "duration:", "stream mapping:", "metadata:", "stream #", "input #", "output #",
                                                       "program ", "encoder", "lavf", "variant_bitrate", "timed_id3", "[hls @", "[mpegts @"
                                                   }

                                                   ' Palavras-chave de erro real
                                                   Dim palavrasErro = New String() {"error", "failed", "unable", "not found"}

                                                   ' Se for um erro crítico real
                                                   If palavrasErro.Any(Function(p) linha.Contains(p)) Then
                                                       hasErrors = True
                                                       Me.Invoke(Sub() txtLog.AppendText("[ERRO] " & ev.Data & Environment.NewLine))

                                                       ' Se for um log técnico de HLS (ignorar a linha real e mostrar o nosso status personalizado)
                                                   ElseIf termosHLS.Any(Function(p) linha.Contains(p)) Then
                                                       Me.Invoke(Sub()
                                                                     Dim tamanho = ObterTamanhoDaPasta(My.Settings.destFolder)
                                                                     Dim tempoGravacao = DateTime.Now - inicioHLS
                                                                     Dim tempoTexto = $"{tempoGravacao.Minutes:D2}:{tempoGravacao.Seconds:D2}"

                                                                     ' Remove a última linha, se já houver
                                                                     If Not String.IsNullOrEmpty(ultimaLinhaHLS) AndAlso txtLog.Text.Contains(ultimaLinhaHLS) Then
                                                                         txtLog.Text = txtLog.Text.Replace(ultimaLinhaHLS, "")
                                                                     End If

                                                                     ' Atualiza com nova linha de status
                                                                     ultimaLinhaHLS = Environment.NewLine & $"📡 Gravando stream HLS... Tempo: {tempoTexto} | Tamanho: {tamanho}" & Environment.NewLine
                                                                     txtLog.AppendText(ultimaLinhaHLS)
                                                                     AtualizarStatus($"Status: Gravando stream HLS... Tempo: {tempoTexto} | Tamanho atual: {tamanho}")
                                                                 End Sub)

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
                                    ultimaLinhaHLS = ""
                                    tcs.TrySetResult(True)
                                    Me.Invoke(Sub()
                                                  StatusLabel.Text = "Status: Pronto..."
                                              End Sub)
                                End Sub

        Try
            inicioHLS = DateTime.Now
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
        Dim sucessoFinal As Boolean = (exitCode = 0 AndAlso Not hasErrors)

        Return sucessoFinal

    End Function
    Private Function ObterTamanhoDaPasta(pasta As String) As String
        Try
            Dim tamanhoTotal As Long = 0
            For Each arquivo In Directory.GetFiles(pasta, "*.part", SearchOption.TopDirectoryOnly)
                tamanhoTotal += New FileInfo(arquivo).Length
            Next

            Return (tamanhoTotal / 1024 / 1024).ToString("0.00") & " MB"
        Catch
            Return "?"
        End Try
    End Function

    Private Function MergeTodosOsVideosEAudios()
        Dim pastaDestino As String = Path.Combine(Application.StartupPath, My.Settings.destFolder)
        Dim arquivosVideo = Directory.GetFiles(pastaDestino, "*_video.*", SearchOption.TopDirectoryOnly)
        Dim arquivosAudio = Directory.GetFiles(pastaDestino, "*_audio.*", SearchOption.TopDirectoryOnly)

        StatusLabel.Text = "Status: Finalizando aguarde..."
        Me.Cursor = Cursors.WaitCursor
        Dim allMergesOK As Boolean = False

        For Each video In arquivosVideo

            Dim nomeBase = Path.GetFileNameWithoutExtension(video).Replace("_video", "")
            Dim audio = arquivosAudio.FirstOrDefault(Function(a) Path.GetFileNameWithoutExtension(a).Replace("_audio", "") = nomeBase)

            'MsgBox($"Unindo {Path.GetFileName(video)} com {If(String.IsNullOrEmpty(audio), "nenhum áudio", Path.GetFileName(audio))}", MsgBoxStyle.Information, "Unindo Vídeo e Áudio")

            If Not String.IsNullOrEmpty(audio) Then
                Dim outputFinal = Path.Combine(pastaDestino, nomeBase & ".mp4")
                If ExecutarMergeSeguro(video, audio, outputFinal) Then
                    File.Delete(video)
                    File.Delete(audio)
                    allMergesOK = True
                Else
                    txtLog.AppendText($"❌ Falha ao fazer merge de: {Path.GetFileName(video)} e {Path.GetFileName(audio)}" & Environment.NewLine)
                End If
            Else
                txtLog.AppendText($"⚠️ Sem áudio correspondente para: {Path.GetFileName(video)}" & Environment.NewLine)
            End If
        Next

        Return allMergesOK

    End Function

    Private Function ExecutarMergeSeguro(video As String, audio As String, outputFinal As String)
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
                If Not ffmpegProc.WaitForExit(300000) Then
                    Try
                        ffmpegProc.Kill()
                        Me.Invoke(Sub() txtLog.AppendText($"[ERRO] ffmpeg travado ao tentar unir: {Path.GetFileName(outputFinal)}. Timeout forçado." & Environment.NewLine))
                        Return False
                    Catch
                        Return False
                    End Try
                Else
                    Return True
                    '  Me.Invoke(Sub() txtLog.AppendText($"✅ Merge concluído: {Path.GetFileName(outputFinal)}" & Environment.NewLine))
                End If
            End Using

        Catch ex As Exception
            Me.Invoke(Sub() txtLog.AppendText($"[ERRO no merge de {Path.GetFileName(outputFinal)}] {ex.Message}" & Environment.NewLine))
            Return False
        End Try
    End Function

    Private Sub OpenFolder()
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
            progressBarDownload.Location = New Point(12, 224)
            Height = 335
        Else
            txtLog.Visible = True
            txtLog.Location = New Point(12, 222)
            progressBarDownload.Location = New Point(12, 407)
            Height = 520
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
        canceladoPeloUsuario = True

        Task.Run(Sub()
                     If processoYtDlp IsNot Nothing AndAlso Not processoYtDlp.HasExited Then
                         Try
                             processoYtDlp.Kill(entireProcessTree:=True)
                             processoYtDlp.WaitForExit(3000)
                             processoYtDlp.Dispose()
                             processoYtDlp = Nothing

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
    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click
        If IO.Directory.Exists(pastaDestino) Then
            Process.Start("explorer.exe", pastaDestino)
        Else
            Process.Start("explorer.exe", Application.StartupPath & "\downloaded")
        End If

    End Sub
    Private Sub Form1_Activated(sender As Object, e As EventArgs) Handles MyBase.Activated
        txtUrl.Focus()
    End Sub
    Private Async Sub VerificarAtualizaçõesToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles VerificarAtualizaçõesToolStripMenuItem.Click
        Await VerificarAtualizacaoYTDLP()
    End Sub

End Class
