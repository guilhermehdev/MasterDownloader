Imports System.IO
Imports System.Collections.Concurrent
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Windows.Forms.LinkLabel
Imports Newtonsoft.Json.Linq

Public Class Form1
    Dim downloadFilePath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PbPb Downloader", "download.txt")
    Dim cookiesFilePath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PbPb Downloader", "cookies.txt")
    Dim archiveFilePath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PbPb Downloader", "archive.txt")

    Private pastaDestino As String = Application.StartupPath
    Private batFilePath As String = Application.StartupPath & "\run.bat"
    Private totalLinks As Integer = 0
    Private linksConcluidos As Integer = 0
    Private totalLinksNaFila As Integer = 0
    Private processoYtDlp As Process = Nothing
    Private ultimaLinhaHLS As String = ""
    Private ultimaLinhaPlaylist As String = ""
    Private inicioHLS As DateTime
    Private progressoAtualLink As Integer = 0
    Private canceladoPeloUsuario As Boolean = False
    Private downloadEmAndamento As Boolean = False
    Private verificacaoAtualizacaoEmAndamento As Boolean = False
    Private ultimoLinkDetectado As String = ""
    Private ReadOnly filaExecucao As New ConcurrentQueue(Of String)()
    Private pendingQueueAdditions As Integer = 0
    Private currentDownloadLink As String = ""
    Private linkAtualEhLive As Boolean = False
    Private encerrandoLive As Boolean = False
    Private liveSalvaAoEncerrar As Boolean = False
    Private liveArquivoEmGravacao As String = ""
    Private ReadOnly liveCapturas As New ConcurrentDictionary(Of String, LiveCaptureJob)(StringComparer.OrdinalIgnoreCase)

    Private Class LiveCaptureJob
        Public Property Link As String = ""
        Public Property Process As Process
        Public Property OutputPath As String = ""
        Public Property StopRequested As Boolean
        Public Property LastLogLine As String = ""
        Public Property Task As Task(Of Boolean)
        Public Property Succeeded As Boolean
        Public Property StartedAt As DateTime
    End Class

    ' --- NOVO: Variáveis para controle de fases dentro de um único link ---
    Private Enum CurrentDownloadPhase
        Initial
        DownloadingPart1 ' Geralmente áudio
        DownloadingPart2 ' Geralmente vídeo
        Merging
        Finalizing
    End Enum
    Private currentLinkPhase As CurrentDownloadPhase = CurrentDownloadPhase.Initial
    ' ----------------------------------------------------------------------
    Private Sub ConfigurarPastaDestino()
        Dim destinoConfigurado = My.Settings.destFolder
        If String.IsNullOrWhiteSpace(destinoConfigurado) Then
            destinoConfigurado = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
            My.Settings.destFolder = destinoConfigurado
            My.Settings.Save()
        End If

        If Path.IsPathRooted(destinoConfigurado) Then
            pastaDestino = Path.GetFullPath(destinoConfigurado)
        Else
            pastaDestino = Path.GetFullPath(Path.Combine(Application.StartupPath, destinoConfigurado))
        End If
        Directory.CreateDirectory(pastaDestino)
    End Sub

    Private Async Function VerificarAtualizacaoAutomaticaYTDLP() As Task
        Dim diretorioDados = Path.GetDirectoryName(downloadFilePath)
        Dim arquivoUltimaVerificacao = Path.Combine(diretorioDados, "yt-dlp-last-check.txt")
        Dim ultimaVerificacao As DateTime

        verificacaoAtualizacaoEmAndamento = True
        btnAdicionar.Enabled = False
        btnExecutar.Enabled = False
        Try
            If File.Exists(arquivoUltimaVerificacao) AndAlso
               DateTime.TryParse(File.ReadAllText(arquivoUltimaVerificacao),
                                 Globalization.CultureInfo.InvariantCulture,
                                 Globalization.DateTimeStyles.RoundtripKind,
                                 ultimaVerificacao) Then
                Dim horasDesdeVerificacao = (DateTime.UtcNow - ultimaVerificacao.ToUniversalTime()).TotalHours
                If horasDesdeVerificacao >= 0 AndAlso horasDesdeVerificacao < 24 Then Return
            End If

            If Await VerificarAtualizacaoYTDLP(silenciosa:=True) Then
                File.WriteAllText(arquivoUltimaVerificacao, DateTime.UtcNow.ToString("O", Globalization.CultureInfo.InvariantCulture))
            End If
        Catch ex As Exception
            ' Uma falha de rede ou de gravação do controle não deve impedir o app de abrir.
            txtLog.AppendText($"[AVISO] Não foi possível concluir a verificação automática do yt-dlp: {ex.Message}{Environment.NewLine}")
        Finally
            verificacaoAtualizacaoEmAndamento = False
            btnAdicionar.Enabled = True
            btnExecutar.Enabled = True
        End Try
    End Function
    Private Async Function addLink(ByVal link As String) As Task
        If verificacaoAtualizacaoEmAndamento OrElse String.IsNullOrWhiteSpace(link) Then Return
        If ListViewContains(lstLink, link) Then
            txtUrl.Clear()
            Return
        End If

        Dim adicionarDuranteExecucao = downloadEmAndamento
        If adicionarDuranteExecucao Then pendingQueueAdditions += 1
        btnAdicionar.Enabled = False
        btnExecutar.Enabled = False
        Me.Cursor = Cursors.WaitCursor
        If Not adicionarDuranteExecucao Then StatusLabel.Text = "Status: Adicionando link..."

        Try
            Directory.CreateDirectory(Path.GetDirectoryName(downloadFilePath))
            If Not File.Exists(downloadFilePath) Then File.WriteAllText(downloadFilePath, String.Empty)
            File.AppendAllText(downloadFilePath, link & Environment.NewLine)
            txtUrl.Clear()

            Dim videoData = Await ContarVideosNaPlaylist(link)
            If LinkPareceLive(link) AndAlso TituloIndicaFalha(videoData.Item2) Then
                RemoverLiveIndisponivel(link)
                Return
            End If
            AdicionarTituloNaListView(UnescapeUnicode(videoData.Item2), link)
            txtLog.AppendText($"📺 Link contém {videoData.Item1} vídeos." & Environment.NewLine)

            If adicionarDuranteExecucao AndAlso downloadEmAndamento AndAlso Not canceladoPeloUsuario Then
                filaExecucao.Enqueue(link)
                totalLinksNaFila += 1
            ElseIf Not downloadEmAndamento Then
                StatusLabel.Text = $"Status: {videoData.Item1} vídeos encontrados"
            End If
        Catch ex As Exception
            If Not downloadEmAndamento Then StatusLabel.Text = "Status: Nenhum video encontrado."
            txtLog.AppendText("❌ Falha ao contar vídeos: " & ex.Message & Environment.NewLine)
        Finally
            If adicionarDuranteExecucao Then pendingQueueAdditions -= 1
            Me.Cursor = Cursors.Default
            btnAdicionar.Enabled = Not verificacaoAtualizacaoEmAndamento
            btnExecutar.Enabled = Not downloadEmAndamento
            If downloadEmAndamento AndAlso liveCapturas.Count > 0 Then AtualizarStatusCapturasLives()
        End Try
    End Function
    Private Async Sub BtnAdicionar_Click(sender As Object, e As EventArgs) Handles btnAdicionar.Click
        Await addLink(txtUrl.Text.Trim())
    End Sub
    Private Sub MarcarItemComoOK(linkOriginal As String)
        AtualizarStatusLink(linkOriginal, "OK")
    End Sub

    Private Sub AtualizarStatusLink(linkOriginal As String, status As String)
        For Each item As ListViewItem In lstLink.Items
            If item.Tag IsNot Nothing AndAlso item.Tag.ToString().Equals(linkOriginal, StringComparison.OrdinalIgnoreCase) Then
                item.SubItems(1).Text = status
                item.SubItems(3).Text = If(status.Equals("Gravando", StringComparison.OrdinalIgnoreCase) OrElse status.Equals("Encerrando", StringComparison.OrdinalIgnoreCase), "⏹", "🗑️")
                Exit For
            End If
        Next
    End Sub

    Private Sub AtualizarDadosLiveNaLista(captura As LiveCaptureJob)
        Dim item = lstLink.Items.Cast(Of ListViewItem)().FirstOrDefault(Function(linha) linha.Tag IsNot Nothing AndAlso linha.Tag.ToString().Equals(captura.Link, StringComparison.OrdinalIgnoreCase))
        If item Is Nothing OrElse item.SubItems.Count <= 2 Then Return

        Dim tempo = DateTime.Now - captura.StartedAt
        Dim tempoTexto = $"{CInt(Math.Floor(tempo.TotalMinutes)):00}:{tempo.Seconds:00}"
        Dim tamanhoTexto = "? MB"
        Try
            If Not String.IsNullOrWhiteSpace(captura.OutputPath) AndAlso File.Exists(captura.OutputPath) Then
                Dim megabytes = New FileInfo(captura.OutputPath).Length / 1024.0 / 1024.0
                tamanhoTexto = $"{megabytes:0.00} MB"
            End If
        Catch
        End Try
        item.SubItems(2).Text = $"{tempoTexto} | {tamanhoTexto}"
    End Sub

    Private Sub AtualizarStatusCapturasLives()
        Dim quantidade = liveCapturas.Count
        If quantidade > 0 Then
            AtualizarStatus("Status: Gravando...")
        ElseIf downloadEmAndamento AndAlso Not canceladoPeloUsuario Then
            AtualizarStatus("Status: Finalizando capturas de live...")
        End If
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
        Dim caminho As String = downloadFilePath
        Try
            File.WriteAllText(caminho, String.Empty)
            txtLog.AppendText(Environment.NewLine & "🧹 Arquivo limpo com sucesso!" & Environment.NewLine)
            lstLink.Items.Clear() ' Se estiver usando ListBox para mostrar os links
            AtualizarStatus("Status: Pronto...")
        Catch ex As Exception
            MessageBox.Show("Erro ao limpar o arquivo: " & ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub
    Private Sub RemoverLinkEspecificoDoArquivo(linkParaRemover As String)
        Try
            Dim caminho As String = downloadFilePath

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

    Private Function LinkPareceLive(link As String) As Boolean
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(link, UriKind.Absolute, uri) Then Return False

        Dim host = uri.DnsSafeHost.TrimEnd("."c).ToLowerInvariant()
        Dim caminho = uri.AbsolutePath.Trim("/"c)
        Dim partes = caminho.Split("/"c, StringSplitOptions.RemoveEmptyEntries)

        If host = "chaturbate.com" OrElse host.EndsWith(".chaturbate.com", StringComparison.Ordinal) Then Return partes.Length > 0
        If host = "twitch.tv" OrElse host.EndsWith(".twitch.tv", StringComparison.Ordinal) Then
            Return partes.Length > 0 AndAlso Not {"videos", "clip", "clips", "directory", "downloads"}.Contains(partes(0).ToLowerInvariant())
        End If
        If host = "kick.com" OrElse host.EndsWith(".kick.com", StringComparison.Ordinal) Then
            Return partes.Length > 0 AndAlso Not {"video", "videos", "categories"}.Contains(partes(0).ToLowerInvariant())
        End If
        If host = "youtube.com" OrElse host = "www.youtube.com" OrElse host.EndsWith(".youtube.com", StringComparison.Ordinal) Then
            Return partes.Length > 0 AndAlso partes(0).Equals("live", StringComparison.OrdinalIgnoreCase)
        End If
        Return False
    End Function

    Private Function TituloIndicaFalha(titulo As String) As Boolean
        Return String.IsNullOrWhiteSpace(titulo) OrElse
               titulo.IndexOf("Título desconhecido", StringComparison.OrdinalIgnoreCase) >= 0 OrElse
               titulo.IndexOf("[ERRO]", StringComparison.OrdinalIgnoreCase) >= 0
    End Function

    Private Sub RemoverLiveIndisponivel(link As String)
        RemoverLinkEspecificoDoArquivo(link)
        For indice = lstLink.Items.Count - 1 To 0 Step -1
            Dim item = lstLink.Items(indice)
            If item.Tag IsNot Nothing AndAlso item.Tag.ToString().Equals(link, StringComparison.OrdinalIgnoreCase) Then
                lstLink.Items.RemoveAt(indice)
            End If
        Next

        Dim filaMantida As New List(Of String)()
        Dim linkNaFila As String = Nothing
        While filaExecucao.TryDequeue(linkNaFila)
            If Not linkNaFila.Equals(link, StringComparison.OrdinalIgnoreCase) Then filaMantida.Add(linkNaFila)
        End While
        For Each linkMantido In filaMantida
            filaExecucao.Enqueue(linkMantido)
        Next

        txtLog.AppendText($"[AVISO] A live não está mais disponível ou já foi encerrada. Link removido: {link}{Environment.NewLine}")
        MessageBox.Show("Esta live não está mais disponível ou já foi encerrada. O link foi removido da lista.", "Live indisponível", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub
    Private Function IsCanal(link As String) As Boolean
        Return link.Contains("youtube.com/@") OrElse link.Contains("youtube.com/c/") OrElse link.Contains("channel/")
    End Function
    Private Async Function BaixarCanal(linkCanal As String) As Task(Of Boolean)
        Dim argsCanal As New StringBuilder()

        If Not linkCanal.Trim().ToLower().EndsWith("/videos") Then
            If linkCanal.Contains("?") Then
                ' Se tiver parâmetros no final (ex: /@canal?sub_confirmation=1), remove e adiciona /videos
                linkCanal = linkCanal.Substring(0, linkCanal.IndexOf("?"))
            End If
            If Not linkCanal.EndsWith("/") Then linkCanal &= "/"
            linkCanal &= "videos"
        End If

        argsCanal.Append("--yes-playlist ")
        argsCanal.Append("--extractor-args ""youtubetab:skip=authcheck"" ")
        argsCanal.Append("--format bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/bestvideo+bestaudio/best/bestvideo ")
        argsCanal.Append("--merge-output-format mp4 ")
        argsCanal.Append($"--cookies ""{cookiesFilePath}"" ")
        argsCanal.Append("--no-warnings ")
        argsCanal.Append("--output """ & pastaDestino & "\%(title)s.%(ext)s"" ")
        argsCanal.Append($"--download-archive ""{archiveFilePath}"" ")
        argsCanal.Append("""" & linkCanal & """ ")

        If Await ExecutarProcessoAsync(txtLog, progressBarDownload, argsCanal.ToString()) Then
            Return True
        Else
            Return False
        End If

    End Function
    Private Function ExtrairCampo(json As String, campo As String) As String
        Dim m = Regex.Match(json, $"""{campo}"":\s*""([^""]+)""")
        If m.Success Then Return m.Groups(1).Value
        Return ""
    End Function

    Private Sub ConverterArgumentosSeparados(info As ProcessStartInfo, linhaDeComando As String)
        Dim argumentos As New List(Of String)()
        Dim indice As Integer = 0

        While indice < linhaDeComando.Length
            While indice < linhaDeComando.Length AndAlso Char.IsWhiteSpace(linhaDeComando(indice))
                indice += 1
            End While
            If indice >= linhaDeComando.Length Then Exit While

            Dim valor As New StringBuilder()
            Dim entreAspas As Boolean = False
            While indice < linhaDeComando.Length
                If linhaDeComando(indice) = "\"c Then
                    Dim inicioBarras = indice
                    While indice < linhaDeComando.Length AndAlso linhaDeComando(indice) = "\"c
                        indice += 1
                    End While
                    Dim quantidadeBarras = indice - inicioBarras
                    If indice < linhaDeComando.Length AndAlso linhaDeComando(indice) = """"c Then
                        valor.Append("\"c, quantidadeBarras \ 2)
                        If quantidadeBarras Mod 2 = 0 Then
                            entreAspas = Not entreAspas
                        Else
                            valor.Append(""""c)
                        End If
                        indice += 1
                    Else
                        valor.Append("\"c, quantidadeBarras)
                    End If
                ElseIf linhaDeComando(indice) = """"c Then
                    entreAspas = Not entreAspas
                    indice += 1
                ElseIf Char.IsWhiteSpace(linhaDeComando(indice)) AndAlso Not entreAspas Then
                    Exit While
                Else
                    valor.Append(linhaDeComando(indice))
                    indice += 1
                End If
            End While

            argumentos.Add(valor.ToString())
        End While

        info.Arguments = String.Empty
        For Each argumento In argumentos
            info.ArgumentList.Add(argumento)
        Next
    End Sub
    Private Async Function ContarVideosNaPlaylist(url As String) As Task(Of (Integer, String))
        Dim ytDlpPath As String = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
        Dim args As String = ""

        If IsCanal(url) Then
            args = $"--dump-json --no-warnings --playlist-items 1 --cookies ""{cookiesFilePath}"" --extractor-args ""youtubetab:skip=authcheck"" ""{url}"""
        Else
            args = $"--dump-json --no-warnings --cookies ""{cookiesFilePath}"" ""{url}"""
        End If

        If String.IsNullOrWhiteSpace(args) Then
            txtLog.AppendText("[ERRO] Argumentos do yt-dlp não foram definidos para a URL fornecida." & Environment.NewLine)
            Return (0, " [ERRO] - Falha ao processar URL")
        End If

        Dim psi As New ProcessStartInfo With {
        .FileName = ytDlpPath,
        .Arguments = args,
        .UseShellExecute = False,
        .RedirectStandardOutput = True,
        .RedirectStandardError = False,
        .CreateNoWindow = True
    }

        Dim jsonSaida As String = ""
        ConverterArgumentosSeparados(psi, args)

        Using proc As Process = Process.Start(psi)
            jsonSaida = Await proc.StandardOutput.ReadToEndAsync()
            Await proc.WaitForExitAsync()
        End Using

        Dim totalVideos As Integer = 1
        Dim titulo As String = "Título desconhecido"
        Dim origem As String = ""
        Dim nomeCanal As String = ""

        Try
            If jsonSaida.Contains("entries") Then
                ' Conta quantos vídeos tem
                Dim entradas = Regex.Matches(jsonSaida, "\{""id"":")
                totalVideos = entradas.Count
            End If

            ' Pega o título da playlist ou vídeo
            'Dim matchTitulo = Regex.Match(jsonSaida, """title""\s*:\s*""(([^""]|\""|\\)*)""")

            'If matchTitulo.Success Then
            '    titulo = matchTitulo.Groups(1).Value
            'End If

            Dim jsonObj As JObject = JObject.Parse(jsonSaida)
            titulo = jsonObj.Value(Of String)("title")

            ' Pega o site de origem
            Dim matchSite = Regex.Match(jsonSaida, """extractor_key"":\s*""([^""]+)""")
            If matchSite.Success Then
                origem = matchSite.Groups(1).Value
            End If

            ' Pega o nome do canal (vários possíveis)
            Dim matchCanal = Regex.Match(jsonSaida, """playlist_uploader"":\s*""([^""]+)""")
            If matchCanal.Success Then
                nomeCanal = matchCanal.Groups(1).Value
            Else
                ' Tenta pegar o uploader
                Dim matchUploader = Regex.Match(jsonSaida, """uploader"":\s*""([^""]+)""")
                If matchUploader.Success Then
                    nomeCanal = matchUploader.Groups(1).Value
                Else
                    ' Tenta pegar o nome do canal de outra forma
                    Dim matchChannelId = Regex.Match(jsonSaida, """channel_id"":\s*""([^""]+)""")
                    If matchChannelId.Success Then
                        nomeCanal = matchChannelId.Groups(1).Value
                    End If
                End If
            End If

            ' Dim caminhoDump As String = Path.Combine(Application.StartupPath, "dump_yt-dlp.json")
            'File.WriteAllText(caminhoDump, jsonSaida)

        Catch ex As Exception
            txtLog.AppendText($"[ERRO JSON] {ex.Message}" & Environment.NewLine)
        End Try

        If IsCanal(url) Then
            Return (Math.Max(1, totalVideos), " [" & origem & "] - Canal: " & nomeCanal)
        Else
            Return (Math.Max(1, totalVideos), " [" & origem & "] - " & titulo)
        End If


    End Function


    'Private Async Function ContarVideosNaPlaylist(url As String) As Task(Of (Integer, String))
    '    Dim ytDlpPath As String = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
    '    Dim args As String = ""

    '    If IsCanal(url) Then
    '        args = $"--dump-json --no-warnings --playlist-items 1 --cookies ""{cookiesFilePath}"" --extractor-args ""youtubetab:skip=authcheck"" ""{url}"""
    '    Else
    '        ' Se for uma playlist normal ou vídeo, usamos o formato padrão
    '        args = $"--dump-json --no-warnings --cookies ""{cookiesFilePath}"" ""{url}"""
    '    End If

    '    If String.IsNullOrWhiteSpace(args) Then
    '        txtLog.AppendText("[ERRO] Argumentos do yt-dlp não foram definidos para a URL fornecida." & Environment.NewLine)
    '        Return (0, " [ERRO] - Falha ao processar URL")
    '    End If

    '    Dim psi As New ProcessStartInfo With {
    '    .FileName = ytDlpPath,
    '    .Arguments = args,
    '    .UseShellExecute = False,
    '    .RedirectStandardOutput = True,
    '    .RedirectStandardError = True,
    '    .CreateNoWindow = True
    '}

    '    Dim jsonSaida As String = ""

    '    Using proc As Process = Process.Start(psi)
    '        jsonSaida = Await proc.StandardOutput.ReadToEndAsync()
    '        proc.WaitForExit()
    '    End Using

    '    Dim totalVideos As Integer = 1
    '    Dim titulo As String = "Título desconhecido"
    '    Dim origem As String = ""
    '    Dim nomeCanal As String = ""

    '    Try
    '        If jsonSaida.Contains("entries") Then
    '            ' É playlist
    '            Dim entradas = Regex.Matches(jsonSaida, "\{""id"":")
    '            totalVideos = entradas.Count
    '        End If

    '        ' Captura o título da playlist ou do vídeo
    '        Dim matchTitulo = Regex.Match(jsonSaida, """title"":\s*""([^""]+)""")
    '        If matchTitulo.Success Then
    '            titulo = matchTitulo.Groups(1).Value
    '        End If

    '        Dim matchSite = Regex.Match(jsonSaida, """extractor_key"":\s*""([^""]+)""")
    '        If matchSite.Success Then
    '            origem = matchSite.Groups(1).Value
    '        End If

    '        Dim matchCanal = Regex.Match(jsonSaida, """channel"":\s*""([^""]+)""")
    '        If matchCanal.Success Then
    '            nomeCanal = matchCanal.Groups(1).Value
    '        Else
    '            ' Fallback para "uploader", caso "channel" não esteja presente
    '            Dim matchUploader = Regex.Match(jsonSaida, """uploader"":\s*""([^""]+)""")
    '            If matchUploader.Success Then
    '                nomeCanal = matchUploader.Groups(1).Value
    '            End If
    '        End If

    '    Catch ex As Exception
    '        txtLog.AppendText($"[ERRO JSON] {ex.Message}" & Environment.NewLine)
    '    End Try

    '    If IsCanal(url) Then
    '        MsgBox(nomeCanal)
    '        Return (Math.Max(1, totalVideos), " [" & origem & "] - Canal: " & nomeCanal)
    '    Else
    '        Return (Math.Max(1, totalVideos), " [" & origem & "] - " & titulo)
    '    End If

    'End Function
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
        item.SubItems.Add("")
        item.SubItems.Add("🗑️")
        lstLink.Items.Add(item)
        TimerClipboard.Start()

    End Sub

    Private Sub lstLink_MouseClick(sender As Object, e As MouseEventArgs) Handles lstLink.MouseClick
        Dim info As ListViewHitTestInfo = lstLink.HitTest(e.Location)

        If info.Item IsNot Nothing AndAlso info.SubItem IsNot Nothing Then
            Dim colunaClicada As Integer = info.Item.SubItems.IndexOf(info.SubItem)

            ' Supondo que a coluna 2 (índice 2) seja a coluna "Ação"
            If colunaClicada = 3 Then
                Dim linkOriginal As String = If(info.Item.Tag Is Nothing, "", info.Item.Tag.ToString())
                If info.Item.SubItems(1).Text.Equals("Gravando", StringComparison.OrdinalIgnoreCase) Then
                    PararLiveDaLinha(linkOriginal)
                    Return
                End If

                If downloadEmAndamento Then Return
                Dim titulo As String = info.Item.Text

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

    Private Async Function VerificarAtualizacaoYTDLP(Optional silenciosa As Boolean = False) As Task(Of Boolean)
        If Not silenciosa Then AtualizarStatus("Status: Verificando atualizações...")
        Dim psi As New ProcessStartInfo With {
            .FileName = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe"),
            .UseShellExecute = False,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True
        }
        ConverterArgumentosSeparados(psi, "--update")

        Try
            Using proc As Process = Process.Start(psi)
                Dim outputTask = proc.StandardOutput.ReadToEndAsync()
                Dim errorsTask = proc.StandardError.ReadToEndAsync()
                Dim waitTask = proc.WaitForExitAsync()
                If Await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(30))) IsNot waitTask Then
                    Try
                        proc.Kill(entireProcessTree:=True)
                    Catch
                    End Try
                    Await waitTask
                    Await outputTask
                    Await errorsTask
                    Throw New TimeoutException("A verificação de atualização excedeu 30 segundos.")
                End If

                Dim output = Await outputTask
                Dim errors = Await errorsTask
                Dim resultado = output & errors
                If proc.ExitCode <> 0 Then
                    txtLog.AppendText($"[ERRO ao atualizar yt-dlp]{Environment.NewLine}{resultado}{Environment.NewLine}")
                    If Not silenciosa Then AtualizarStatus("Status: Falha ao atualizar o yt-dlp. Consulte o log.")
                    Return False
                End If

                If resultado.IndexOf("Updated yt-dlp", StringComparison.OrdinalIgnoreCase) >= 0 Then
                    txtLog.AppendText($"[yt-dlp atualizado]{Environment.NewLine}{resultado}{Environment.NewLine}")
                    If Not silenciosa Then AtualizarStatus("Status: yt-dlp atualizado.")
                ElseIf Not silenciosa Then
                    txtLog.AppendText($"[Verificação de atualização yt-dlp]{Environment.NewLine}{resultado}{Environment.NewLine}")
                    AtualizarStatus("Status: yt-dlp já está atualizado.")
                End If
                Return True
            End Using
        Catch ex As Exception
            txtLog.AppendText($"[ERRO ao atualizar yt-dlp] {ex.Message}{Environment.NewLine}")
            If Not silenciosa Then AtualizarStatus("Status: Falha ao atualizar o yt-dlp.")
            Return False
        Finally
            If Not silenciosa Then AtualizarStatus("Status: Pronto...")
        End Try
    End Function
    Private Async Function CarregarLegendasDisponiveis(link As String) As Task
        Dim psi As New ProcessStartInfo With {
        .FileName = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe"),
        .Arguments = $"--list-subs --cookies ""{cookiesFilePath}"" --no-warnings ""{link}""",
        .UseShellExecute = False,
        .RedirectStandardOutput = True,
        .RedirectStandardError = True,
        .CreateNoWindow = True,
        .RedirectStandardInput = True
    }

        ConverterArgumentosSeparados(psi, $"--list-subs --cookies ""{cookiesFilePath}"" --no-warnings ""{link}""")
        Using proc As Process = Process.Start(psi)
            processoYtDlp = proc
            Dim outputTask = proc.StandardOutput.ReadToEndAsync()
            Dim errorsTask = proc.StandardError.ReadToEndAsync()
            Try
                Dim output As String = Await outputTask
                Dim errors As String = Await errorsTask
                Await proc.WaitForExitAsync()
                If canceladoPeloUsuario Then Return
                proc.Close()

                ' txtLog.AppendText(Environment.NewLine & $"[Legendas disponíveis]{Environment.NewLine}{output}{errors}{Environment.NewLine}")

                Me.Invoke(Sub()
                              FormLegendas.cmbLegendas.Items.Clear()

                              Dim linhas = output.Split({Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries)

                              Dim startParsing As Boolean = False

                              For Each linha In linhas
                                  linha = linha.Trim()

                                  ' Começa só depois do cabeçalho "Language formats"
                                  If linha.StartsWith("Language") Then
                                      startParsing = True
                                      Continue For
                                  End If

                                  If Not startParsing Then Continue For

                                  ' Filtro: Só pega linhas que comecem com código de idioma válido (ex: en, pt, es, etc)
                                  If System.Text.RegularExpressions.Regex.IsMatch(linha, "^[a-z]{2}(\-[a-z]{2})?\s", RegexOptions.IgnoreCase) Then
                                      Dim partes = linha.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
                                      If partes.Length > 0 AndAlso Not FormLegendas.cmbLegendas.Items.Contains(partes(0)) Then
                                          FormLegendas.cmbLegendas.Items.Add(partes(0)) ' Exemplo: "en", "pt", "es"
                                      End If
                                  End If
                              Next

                              If FormLegendas.cmbLegendas.Items.Count > 0 Then
                                  FormLegendas.cmbLegendas.SelectedIndex = 0
                              Else
                                  FormLegendas.cmbLegendas.Items.Add("auto (gerada automaticamente)")
                                  'FormLegendas.cmbLegendas.SelectedIndex = 0
                              End If
                          End Sub)
            Finally
                If Object.ReferenceEquals(processoYtDlp, proc) Then processoYtDlp = Nothing
            End Try
        End Using
    End Function

    Private Async Function DetectarMelhorFormato(link As String) As Task(Of String)
        Dim psi As New ProcessStartInfo With {
        .FileName = "app\yt-dlp.exe",
        .Arguments = $"-F ""{link}""",
        .RedirectStandardOutput = True,
        .RedirectStandardError = False,
        .UseShellExecute = False,
        .CreateNoWindow = True
    }

        Dim formatosDisponiveis As New List(Of String)
        ConverterArgumentosSeparados(psi, $"-F ""{link}""")
        Using proc As Process = Process.Start(psi)
            While Not proc.StandardOutput.EndOfStream
                Dim linha As String = Await proc.StandardOutput.ReadLineAsync()
                If linha IsNot Nothing AndAlso linha.Contains("mp4") Then
                    formatosDisponiveis.Add(linha.ToLower())
                End If
            End While
            Await proc.WaitForExitAsync()
        End Using

        Dim temVideoMp4 As Boolean = formatosDisponiveis.Any(Function(l) l.Contains("video") AndAlso l.Contains("mp4"))
        Dim temAudioM4a As Boolean = formatosDisponiveis.Any(Function(l) l.Contains("audio") AndAlso l.Contains("m4a"))
        Dim temBestMp4 As Boolean = formatosDisponiveis.Any(Function(l) l.Contains("mp4") AndAlso l.Contains("best"))

        If temVideoMp4 AndAlso temAudioM4a Then
            Return "--format ""bestvideo[ext=mp4]+bestaudio[ext=m4a]"""
        ElseIf temBestMp4 Then
            Return "--format ""best[ext=mp4]"""
        Else
            Return "--format ""best"""
        End If
    End Function
    Private Sub LimparArquivoDeHistorico()
        Dim archivePath = archiveFilePath
        If File.Exists(archivePath) Then
            Try
                File.WriteAllText(archivePath, String.Empty)
                txtLog.AppendText("🧹 Histórico de vídeos baixados limpo com sucesso." & Environment.NewLine)
            Catch ex As Exception
                txtLog.AppendText("[ERRO] Falha ao limpar o arquivo archive.txt: " & ex.Message & Environment.NewLine)
            End Try
        End If
    End Sub


    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Directory.CreateDirectory(Path.GetDirectoryName(downloadFilePath))
        If Not File.Exists(downloadFilePath) Then File.WriteAllText(downloadFilePath, String.Empty)
        ConfigurarPastaDestino()
        Await VerificarAtualizacaoAutomaticaYTDLP()
        NotifyIcon1.Text = "PbPb Downloader"
        progressBarDownload.Location = New Point(12, 224)
        Me.Height = 335
        lstLink.Columns.Add("Título", 240)
        lstLink.Columns.Add("Status", 80)
        lstLink.Columns.Add("Tempo | Tamanho", 120)
        lstLink.Columns.Add("Action", 40)

        If File.Exists(downloadFilePath) Then
            AtualizarStatus("Status: Processando links...")
            btnExecutar.Enabled = False
            Dim links = File.ReadAllLines(downloadFilePath).Where(Function(l) Not String.IsNullOrWhiteSpace(l)).ToArray()

            For Each link In links
                Dim videoData = Await ContarVideosNaPlaylist(link)
                If LinkPareceLive(link) AndAlso TituloIndicaFalha(videoData.Item2) Then
                    RemoverLiveIndisponivel(link)
                    Continue For
                End If
                AdicionarTituloNaListView(UnescapeUnicode(videoData.Item2), link)
                TimerClipboard.Stop()
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
        End If
        AtualizarStatus("Status: Pronto...")
        btnExecutar.Enabled = True
        TimerClipboard.Start()

    End Sub
    Private Sub BtLimparLista_Click(sender As Object, e As EventArgs) Handles btLimparLista.Click
        If downloadEmAndamento Then Return
        LimparArquivoDownload()
    End Sub
    Private Async Function ObterMetadadosLinkAsync(link As String) As Task(Of JObject)
        Dim ytDlpPath = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe")
        Dim psi As New ProcessStartInfo With {
            .FileName = ytDlpPath,
            .Arguments = $"--dump-single-json --skip-download --playlist-items 1 --no-warnings --cookies ""{cookiesFilePath}"" ""{link}""",
            .UseShellExecute = False,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True
        }

        ConverterArgumentosSeparados(psi, $"--dump-single-json --skip-download --playlist-items 1 --no-warnings --cookies ""{cookiesFilePath}"" ""{link}""")
        Using proc As New Process With {.StartInfo = psi}
            proc.Start()
            processoYtDlp = proc
            Dim outputTask = proc.StandardOutput.ReadToEndAsync()
            Dim errorTask = proc.StandardError.ReadToEndAsync()
            Dim waitTask = proc.WaitForExitAsync()
            Try
                If Await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(30))) IsNot waitTask Then
                    proc.Kill(entireProcessTree:=True)
                    Await waitTask
                    Await outputTask
                    Await errorTask
                    Throw New TimeoutException("A consulta dos dados do link excedeu 30 segundos.")
                End If

                Dim output = Await outputTask
                Dim errors = Await errorTask
                If canceladoPeloUsuario Then Throw New OperationCanceledException("Consulta cancelada pelo usuário.")
                If proc.ExitCode <> 0 Then
                    Throw New InvalidOperationException(If(String.IsNullOrWhiteSpace(errors), "yt-dlp não conseguiu ler os dados do link.", errors.Trim()))
                End If
                Return JObject.Parse(output)
            Finally
                If Object.ReferenceEquals(processoYtDlp, proc) Then processoYtDlp = Nothing
            End Try
        End Using
    End Function
    Private Function MetadadosIndicamLive(metadados As JObject) As Boolean
        Return String.Equals(CStr(metadados("live_status")), "is_live", StringComparison.OrdinalIgnoreCase) OrElse
               (metadados("is_live") IsNot Nothing AndAlso metadados("is_live").Value(Of Boolean)())
    End Function

    Private Function MetadadosIndicamPlaylist(metadados As JObject) As Boolean
        Return String.Equals(CStr(metadados("_type")), "playlist", StringComparison.OrdinalIgnoreCase)
    End Function
    Private Function onlyAudio(link)
        Dim argsAudio As New StringBuilder()
        argsAudio.Append("--extract-audio --audio-format mp3 ")
        argsAudio.Append("--format bestaudio/best ")
        argsAudio.Append($"--output ""{pastaDestino}\%(title)s.%(ext)s"" ""{link}"" ")
        argsAudio.Append("--ignore-errors ")
        argsAudio.Append($"--cookies ""{cookiesFilePath}"" ")
        ' argsAudio.Append("--cookies-from-browser chrome ")
        argsAudio.Append("--no-warnings ")

        Return argsAudio

    End Function

    Public Function argsLegend(args As String)
        Dim argsLeg As New StringBuilder()
        argsLeg.Append(args)
        Return argsLeg
    End Function

    Private Async Sub BtnExecutar_Click(sender As Object, e As EventArgs) Handles btnExecutar.Click
        If downloadEmAndamento Then Return
        btnExecutar.Enabled = False
        btCancelar.Enabled = True
        'timerFakeProgress.Start()
        progressBarDownload.Value = 0
        StatusLabel.Text = "Status: Iniciando..."
        Me.Cursor = Cursors.WaitCursor
        txtLog.Clear()

        Dim successOverall As Boolean = True ' Para rastrear se todos os downloads tiveram sucesso
        Dim liveCaptureJobs As New List(Of LiveCaptureJob)()

        Dim linksList As New List(Of String)()
        If File.Exists(downloadFilePath) Then
            linksList = File.ReadAllLines(downloadFilePath).Where(Function(l) Not String.IsNullOrWhiteSpace(l)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        End If
        Dim linkDescartado As String = Nothing
        While filaExecucao.TryDequeue(linkDescartado)
        End While
        For Each linkInicial In linksList
            filaExecucao.Enqueue(linkInicial)
        Next
        pendingQueueAdditions = 0

        If linksList.Count = 0 And lstLink.Items.Count = 0 Then
            MessageBox.Show("Nenhum link válido encontrado.")
            txtLog.AppendText(Environment.NewLine & "❌ Nenhum link válido encontrado." & Environment.NewLine)
            btnExecutar.Enabled = True
            btCancelar.Enabled = False
            timerFakeProgress.Stop()
            Me.Cursor = Cursors.Default
            StatusLabel.Text = "Status: Pronto"
            Exit Sub
        End If

        linksConcluidos = 0
        totalLinksNaFila = linksList.Count
        liveSalvaAoEncerrar = False
        encerrandoLive = False
        canceladoPeloUsuario = False


        ' AQUI MUDAMOS A LÓGICA DO PROGRESSBAR.MAXIMUM
        ' Se cada link pode ter 2 etapas (audio + video + merging),
        ' e para cada etapa você quer 100%, então o máximo é linksList.Count * 100
        ' ou linksList.Count * 200, se você quiser dividir o progresso de download entre as duas partes
        ' Para simplificar, vou manter 100% por link e gerenciar internamente.
        progressBarDownload.Maximum = 100 ' O progresso será por link, de 0 a 100%

        Try
            downloadEmAndamento = True
            btnAdicionar.Enabled = Not verificacaoAtualizacaoEmAndamento
            btLimparLista.Enabled = False
            TimerClipboard.Stop()
            While True
                If canceladoPeloUsuario Then Exit While
                Dim link As String = Nothing
                If Not filaExecucao.TryDequeue(link) Then
                    Dim tarefasLivesAtivas = liveCapturas.Values.Select(Function(jobState) jobState.Task).Where(Function(tarefa) tarefa IsNot Nothing).ToArray()
                    If tarefasLivesAtivas.Length > 0 Then
                        Await Task.WhenAny(Task.WhenAny(tarefasLivesAtivas), Task.Delay(250))
                        Continue While
                    ElseIf pendingQueueAdditions > 0 Then
                        Await Task.Delay(200)
                        Continue While
                    Else
                        Exit While
                    End If
                End If
                currentDownloadLink = link
                linkAtualEhLive = False
                liveArquivoEmGravacao = ""
                Dim linkOriginal As String = link ' Mantém o link original para referência

                ' Consultar metadados uma vez, sem bloquear a interface, e reutilizá-los abaixo.
                Dim metadados As JObject = Nothing
                If Not IsCanal(link) Then
                    Try
                        metadados = Await ObterMetadadosLinkAsync(link)
                    Catch ex As Exception
                        If canceladoPeloUsuario Then Exit While
                        If LinkPareceLive(link) Then
                            RemoverLiveIndisponivel(link)
                            successOverall = False
                            Continue While
                        End If
                        txtLog.AppendText($"[ERRO ao consultar o link] {ex.Message}{Environment.NewLine}")
                        successOverall = False
                        Continue While
                    End Try
                End If
                Dim linkEhLive = metadados IsNot Nothing AndAlso MetadadosIndicamLive(metadados)
                Dim linkEhPlaylist = metadados IsNot Nothing AndAlso MetadadosIndicamPlaylist(metadados)
                linkAtualEhLive = linkEhLive

                If Not linkEhLive AndAlso liveCapturas.Count > 0 Then
                    Dim tarefasAtivas = liveCapturas.Values.Select(Function(jobState) jobState.Task).Where(Function(tarefa) tarefa IsNot Nothing).ToArray()
                    If tarefasAtivas.Length > 0 Then Await Task.WhenAll(tarefasAtivas)
                    If canceladoPeloUsuario Then Exit While
                End If

                progressBarDownload.Value = 0
                If linkEhPlaylist Then
                    AtualizarStatus("Status: Baixando playlist...")
                Else
                    AtualizarStatus($"Status: Baixando {linksConcluidos} de {totalLinksNaFila}...")
                End If
                Dim args As New StringBuilder()
                ' Definindo argumentos base para yt-dlp
                args.Append($"--output ""{pastaDestino}\%(title)s.%(ext)s"" ""{link}"" ")
                args.Append($"--cookies ""{cookiesFilePath}"" ")
                args.Append("--no-warnings ")
                args.Append("--progress --newline --no-mtime ") ' Manter essas para o parser

                If IsCanal(link) Then
                    ' Se for um canal, vamos baixar todos os vídeos
                    If Await BaixarCanal(link) Then
                        MarcarItemComoOK(linkOriginal)
                        linksConcluidos += 1
                        Dim resposta = MessageBox.Show($"Excluir histórico de videos baixados?", "Atenção", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
                        If resposta = DialogResult.Yes Then
                            LimparArquivoDeHistorico()
                        End If
                    Else
                        successOverall = False
                    End If
                    Continue While ' Próximo link
                End If

                If linkEhLive Then
                    args.Clear()
                    args.Append($"--output ""{pastaDestino}\%(title)s [%(id)s].%(ext)s"" ""{link}"" ")
                    args.Append($"--cookies ""{cookiesFilePath}"" --no-warnings --progress --newline --no-mtime ")
                    ' Use o downloader HLS nativo do yt-dlp: o downloader externo do FFmpeg pode perder
                    ' segmentos enquanto acompanha playlists HLS ao vivo, causando saltos/travamentos.
                    args.Append("--format best/bestvideo+bestaudio/bestvideo --buffer-size 1M --fragment-retries 20 --retry-sleep fragment:exp=1:30 --hls-use-mpegts --no-part ")
                    While liveCapturas.Count >= 2 AndAlso Not canceladoPeloUsuario
                        Dim tarefasAtivas = liveCapturas.Values.Select(Function(jobState) jobState.Task).Where(Function(tarefa) tarefa IsNot Nothing).ToArray()
                        If tarefasAtivas.Length = 0 Then Exit While
                        Await Task.WhenAny(tarefasAtivas)
                    End While
                    If canceladoPeloUsuario Then Exit While

                    Dim captura As New LiveCaptureJob With {.Link = linkOriginal}
                    liveCapturas(linkOriginal) = captura
                    liveCaptureJobs.Add(captura)
                    AtualizarStatusLink(linkOriginal, "Gravando")
                    Me.Cursor = Cursors.Default
                    chkLegendas.Enabled = False
                    CheckBoxAudio.Enabled = False
                    AtualizarStatus("Status: Gravando...")
                    captura.Task = ExecutarCapturaLiveAsync(captura, args.ToString())
                    Continue While ' Próximo link
                End If

                ' Lógica para Playlists e Vídeos/Áudio individuais
                If linkEhPlaylist Then
                    If CheckBoxAudio.Checked Then
                        args = onlyAudio(link) ' Já inclui o --output e outras configs
                    Else
                        args.Append("--extractor-args ""youtubetab:skip=authcheck"" ")
                        args.Append("--format bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/bestvideo+bestaudio/best/bestvideo ") ' Tenta mesclar em MP4
                    End If
                Else ' Single Video
                    If CheckBoxAudio.Checked Then
                        args = onlyAudio(link)
                    Else
                        args.Append("--extractor-args ""youtubetab:skip=authcheck"" ")
                        args.Append("--format bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/bestvideo+bestaudio/best/bestvideo --no-playlist ")
                        args.Append("--merge-output-format mp4 ") ' Garante saída MP4 se houver fusão
                    End If
                End If

                ' Lógica de Legendas
                If chkLegendas.Checked AndAlso Not CheckBoxAudio.Checked Then ' Legendas só fazem sentido para vídeo
                    Await CarregarLegendasDisponiveis(link)
                    If canceladoPeloUsuario Then Exit While
                    Dim resultado As DialogResult = FormLegendas.ShowDialog()
                    If resultado = DialogResult.OK AndAlso Not String.IsNullOrEmpty(FormLegendas.args) Then
                        args.Append(" " & FormLegendas.args & " ")
                    End If
                End If

                ' Agora executamos o processo para o link atual
                If canceladoPeloUsuario Then Exit While ' Verifica cancelamento antes de executar
                If Await ExecutarProcessoAsync(txtLog, progressBarDownload, args.ToString()) Then
                    MarcarItemComoOK(linkOriginal)
                    linksConcluidos += 1
                Else
                    successOverall = False
                End If
            End While

            Dim tarefasLives = liveCaptureJobs.Select(Function(captura) captura.Task).Where(Function(tarefa) tarefa IsNot Nothing).ToArray()
            If tarefasLives.Length > 0 Then Await Task.WhenAll(tarefasLives)
            If liveCaptureJobs.Any(Function(captura) Not captura.Succeeded) Then successOverall = False

            ' --- Finalização ---
            If liveSalvaAoEncerrar Then
                StatusLabel.Text = "Status: Live encerrada e salva."
                txtLog.AppendText("✅ As capturas de live encerradas foram salvas." & Environment.NewLine)
                OpenFolder()
            ElseIf Not canceladoPeloUsuario And successOverall Then
                txtLog.AppendText(Environment.NewLine & "✅ Todos os arquivos baixados com sucesso!" & Environment.NewLine)
                OpenFolder()
                StatusLabel.Text = "Status: Pronto"
                Me.Cursor = Cursors.Default
            ElseIf canceladoPeloUsuario Then
                StatusLabel.Text = "Status: Download cancelado pelo usuário."
                txtLog.AppendText(Environment.NewLine & "⚠️ Download cancelado pelo usuário." & Environment.NewLine)
            Else
                StatusLabel.Text = "Status: Download falhou."
                txtLog.AppendText(Environment.NewLine & "❌ Download falhou para um ou mais links." & Environment.NewLine)
            End If

        Catch ex As Exception
            txtLog.AppendText(Environment.NewLine & $"[ERRO INESPERADO] {ex.Message}")
            StatusLabel.Text = "Status: Falha no download..."
            NotifyIcon1.BalloonTipTitle = "❌ Download Falhou"
            NotifyIcon1.BalloonTipText = $"Ocorreu uma falha durante o download."
            NotifyIcon1.ShowBalloonTip(2000)
            successOverall = False
        Finally
            btnExecutar.Enabled = True
            btCancelar.Enabled = False
            progressBarDownload.Value = 0 ' Reseta a barra de progresso ao finalizar
            timerFakeProgress.Stop() ' Certifique-se de parar o timer
            Me.Invoke(Sub()
                          Me.Cursor = Cursors.Default
                          txtLog.Cursor = Cursors.Default
                          chkLegendas.Enabled = True ' Reabilita
                          CheckBoxAudio.Enabled = True ' Reabilita
                          btLimparLista.Enabled = True ' Reabilita
                      End Sub)

            If liveSalvaAoEncerrar Then
                NotifyIcon1.BalloonTipTitle = "✅ Live salva"
                NotifyIcon1.BalloonTipText = "A captura da live foi encerrada e salva."
                NotifyIcon1.ShowBalloonTip(2000)
            ElseIf successOverall AndAlso Not canceladoPeloUsuario Then
                NotifyIcon1.BalloonTipTitle = "✅ Download Concluído"
                NotifyIcon1.BalloonTipText = $"Todos os arquivos foram baixados com sucesso."
                NotifyIcon1.ShowBalloonTip(2000)
            ElseIf canceladoPeloUsuario Then
                NotifyIcon1.BalloonTipTitle = "⛔ Download Cancelado"
                NotifyIcon1.BalloonTipText = $"O processo foi cancelado pelo usuário."
                NotifyIcon1.ShowBalloonTip(2000)
            End If
            NotifyIcon1.Text = "PbPb Downloader"
            canceladoPeloUsuario = False
            downloadEmAndamento = False
            btnAdicionar.Enabled = True
            TimerClipboard.Start()
        End Try
    End Sub

    ' Executado na thread da interface: substitui apenas a linha de progresso anterior.
    Private Sub AtualizarLinhaProgresso(logTextBox As TextBox, linha As String, ByRef linhaAnterior As String)
        Dim novaLinha = linha & Environment.NewLine
        Dim indice = If(String.IsNullOrEmpty(linhaAnterior), -1,
                        logTextBox.Text.IndexOf(linhaAnterior, StringComparison.Ordinal))

        If indice >= 0 Then
            logTextBox.Select(indice, linhaAnterior.Length)
            logTextBox.SelectedText = novaLinha
        Else
            logTextBox.AppendText(novaLinha)
        End If

        linhaAnterior = novaLinha
        logTextBox.SelectionStart = logTextBox.TextLength
        logTextBox.SelectionLength = 0
        logTextBox.ScrollToCaret()
    End Sub

    Private Function CapturaLiveFoiSalva() As Boolean
        Try
            Return Not String.IsNullOrWhiteSpace(liveArquivoEmGravacao) AndAlso
                   File.Exists(liveArquivoEmGravacao) AndAlso
                   New FileInfo(liveArquivoEmGravacao).Length >= 188
        Catch
            Return False
        End Try
    End Function

    Private Async Function GerarMiniaturaLiveAsync(caminhoVideo As String) As Task(Of String)
        Dim caminhoImagem As String = ""
        Try
            If Not File.Exists(caminhoVideo) Then Return ""

            Dim pasta = Path.GetDirectoryName(caminhoVideo)
            Dim nomeBase = Path.GetFileNameWithoutExtension(caminhoVideo)
            caminhoImagem = Path.Combine(pasta, nomeBase & "_thumb.jpg")
            Dim indice = 2
            While File.Exists(caminhoImagem)
                caminhoImagem = Path.Combine(pasta, $"{nomeBase}_thumb_{indice}.jpg")
                indice += 1
            End While

            Dim psi As New ProcessStartInfo With {
                .FileName = Path.Combine(Application.StartupPath, "app", "ffmpeg.exe"),
                .UseShellExecute = False,
                .RedirectStandardError = True,
                .CreateNoWindow = True
            }
            For Each argumento In {"-hide_banner", "-loglevel", "error", "-ss", "00:00:05", "-i", caminhoVideo, "-frames:v", "1", "-q:v", "2", caminhoImagem}
                psi.ArgumentList.Add(argumento)
            Next

            Using proc As Process = Process.Start(psi)
                Dim errorsTask = proc.StandardError.ReadToEndAsync()
                Dim waitTask = proc.WaitForExitAsync()
                If Await Task.WhenAny(waitTask, Task.Delay(TimeSpan.FromSeconds(30))) IsNot waitTask Then
                    Try
                        proc.Kill(entireProcessTree:=True)
                    Catch
                    End Try
                    Await waitTask
                    Await errorsTask
                    Throw New TimeoutException("A geração da miniatura excedeu 30 segundos.")
                End If

                Dim errors = Await errorsTask
                If proc.ExitCode = 0 AndAlso File.Exists(caminhoImagem) AndAlso New FileInfo(caminhoImagem).Length > 0 Then
                    Return caminhoImagem
                End If
                Throw New InvalidOperationException(If(String.IsNullOrWhiteSpace(errors), "O FFmpeg não encontrou um quadro para a miniatura.", errors.Trim()))
            End Using
        Catch ex As Exception
            If Not String.IsNullOrWhiteSpace(caminhoImagem) AndAlso File.Exists(caminhoImagem) Then
                Try
                    File.Delete(caminhoImagem)
                Catch
                End Try
            End If
            txtLog.AppendText($"[AVISO] Não foi possível gerar a miniatura da live: {ex.Message}{Environment.NewLine}")
            Return ""
        End Try
    End Function

    Private Async Function ExecutarCapturaLiveAsync(captura As LiveCaptureJob, argumentos As String) As Task(Of Boolean)
        Dim stdoutConcluido As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim stderrConcluido As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim psi As New ProcessStartInfo With {
            .FileName = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe"),
            .WorkingDirectory = Application.StartupPath,
            .UseShellExecute = False,
            .RedirectStandardOutput = True,
            .RedirectStandardError = True,
            .CreateNoWindow = True
        }
        ConverterArgumentosSeparados(psi, argumentos)

        Dim proc As New Process With {.StartInfo = psi, .EnableRaisingEvents = True}
        captura.Process = proc
        Dim registrarLinha As Action(Of String, Boolean) =
            Sub(linha, substituir)
                If String.IsNullOrWhiteSpace(linha) OrElse IsDisposed Then Return
                Try
                    BeginInvoke(Sub()
                                    Dim titulo = lstLink.Items.Cast(Of ListViewItem)().FirstOrDefault(Function(item) item.Tag IsNot Nothing AndAlso item.Tag.ToString().Equals(captura.Link, StringComparison.OrdinalIgnoreCase))?.Text
                                    Dim texto = $"[Live: {If(String.IsNullOrWhiteSpace(titulo), captura.Link, titulo)}] {linha}"
                                    AtualizarDadosLiveNaLista(captura)
                                    If substituir AndAlso Not String.IsNullOrEmpty(captura.LastLogLine) Then
                                        txtLog.Text = txtLog.Text.Replace(captura.LastLogLine & Environment.NewLine, "")
                                    End If
                                    txtLog.AppendText(texto & Environment.NewLine)
                                    captura.LastLogLine = If(substituir, texto, "")
                                End Sub)
                Catch
                End Try
            End Sub

        Dim lerLinha As Action(Of String) =
            Sub(linha)
                If String.IsNullOrWhiteSpace(linha) Then Return
                Dim marcador = "[download] Destination:"
                Dim indice = linha.IndexOf(marcador, StringComparison.Ordinal)
                If indice >= 0 Then captura.OutputPath = linha.Substring(indice + marcador.Length).Trim().Trim(""""c)
                Dim progresso = linha.StartsWith("[download]", StringComparison.OrdinalIgnoreCase) AndAlso Regex.IsMatch(linha, "\d{1,3}(?:\.\d+)?%")
                registrarLinha(linha.Trim(), progresso)
            End Sub

        AddHandler proc.OutputDataReceived, Sub(s, ev)
                                                If ev.Data Is Nothing Then
                                                    stdoutConcluido.TrySetResult(True)
                                                Else
                                                    lerLinha(ev.Data)
                                                End If
                                            End Sub
        AddHandler proc.ErrorDataReceived, Sub(s, ev)
                                               If ev.Data Is Nothing Then
                                                   stderrConcluido.TrySetResult(True)
                                               Else
                                                   lerLinha(ev.Data)
                                               End If
                                           End Sub

        Try
            captura.StartedAt = DateTime.Now
            proc.Start()
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()
            Await proc.WaitForExitAsync()
            Await Task.WhenAll(stdoutConcluido.Task, stderrConcluido.Task)

            Dim capturaRemovida As LiveCaptureJob = Nothing
            liveCapturas.TryRemove(captura.Link, capturaRemovida)
            AtualizarStatusCapturasLives()

            Dim arquivoSalvo = Not String.IsNullOrWhiteSpace(captura.OutputPath) AndAlso
                               File.Exists(captura.OutputPath) AndAlso New FileInfo(captura.OutputPath).Length >= 188
            AtualizarDadosLiveNaLista(captura)
            If captura.StopRequested AndAlso arquivoSalvo Then
                captura.Succeeded = True
                liveSalvaAoEncerrar = True
                linksConcluidos += 1
                AtualizarStatusLink(captura.Link, "Salvo")
                txtLog.AppendText($"[Live salva] Captura encerrada em: {captura.OutputPath}{Environment.NewLine}")
                Dim miniatura = Await GerarMiniaturaLiveAsync(captura.OutputPath)
                If Not String.IsNullOrWhiteSpace(miniatura) Then txtLog.AppendText($"[Miniatura] Imagem salva em: {miniatura}{Environment.NewLine}")
            ElseIf proc.ExitCode = 0 Then
                captura.Succeeded = True
                linksConcluidos += 1
                MarcarItemComoOK(captura.Link)
                txtLog.AppendText($"[Live concluída] {captura.OutputPath}{Environment.NewLine}")
                If arquivoSalvo Then
                    Dim miniatura = Await GerarMiniaturaLiveAsync(captura.OutputPath)
                    If Not String.IsNullOrWhiteSpace(miniatura) Then txtLog.AppendText($"[Miniatura] Imagem salva em: {miniatura}{Environment.NewLine}")
                End If
            Else
                AtualizarStatusLink(captura.Link, "Erro")
                txtLog.AppendText($"[ERRO] A captura da live terminou com código {proc.ExitCode}.{Environment.NewLine}")
            End If
            Return captura.Succeeded
        Catch ex As Exception
            AtualizarStatusLink(captura.Link, "Erro")
            txtLog.AppendText($"[ERRO na live] {ex.Message}{Environment.NewLine}")
            Return False
        Finally
            captura.Process = Nothing
            proc.Dispose()
            Dim removida As LiveCaptureJob = Nothing
            liveCapturas.TryRemove(captura.Link, removida)
            AtualizarStatusCapturasLives()
        End Try
    End Function

    Private Sub PararLiveDaLinha(link As String)
        Dim captura As LiveCaptureJob = Nothing
        If Not liveCapturas.TryGetValue(link, captura) OrElse captura.StopRequested Then Return
        captura.StopRequested = True
        AtualizarStatusLink(link, "Encerrando")
        txtLog.AppendText($"[Live] Encerrando e salvando: {link}{Environment.NewLine}")
        Try
            Dim proc = captura.Process
            If proc IsNot Nothing AndAlso Not proc.HasExited Then proc.Kill(entireProcessTree:=True)
        Catch ex As InvalidOperationException
        Catch ex As Exception
            captura.StopRequested = False
            AtualizarStatusLink(link, "Gravando")
            txtLog.AppendText($"[ERRO ao encerrar a live] {ex.Message}{Environment.NewLine}")
        End Try
    End Sub

    ' --- ExecutarProcessoAsync Modificado ---
    Public Async Function ExecutarProcessoAsync(ByVal logTextBox As TextBox, ByVal progressBar As ProgressBar, ByVal argumentos As String) As Task(Of Boolean)

        Dim ultimaLinhaDownload As String = ""
        Dim estimador As New EstimadorEta()
        Dim relogioEta = Stopwatch.StartNew()
        Dim arquivosDestinoDoLink As New ConcurrentDictionary(Of String, Byte)(StringComparer.OrdinalIgnoreCase)
        Dim stdoutConcluido As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim stderrConcluido As New TaskCompletionSource(Of Boolean)(TaskCreationOptions.RunContinuationsAsynchronously)
        Dim hasErrors As Boolean = False
        Dim avisoMoovExibido As Boolean = False
        Dim aguardandoComplementoMoov As Boolean = False
        Dim exitCode As Integer = -1
        Dim ignorandoListaLegendas As Boolean = False ' Variável para controlar o estado de ignorar logs de legenda
        currentLinkPhase = CurrentDownloadPhase.Initial ' Resetar a fase para cada novo link

        Dim psi As New ProcessStartInfo With {
            .FileName = IO.Path.Combine(Application.StartupPath, "app", "yt-dlp.exe") ' Caminho para yt-dlp
        }

        ' Adicionar --progress e --newline se já não estiver nos argumentos
        Dim argumentosProcesso = argumentos
        If Not argumentos.Contains("--progress") Then argumentosProcesso &= " --progress"
        If Not argumentos.Contains("--newline") Then argumentosProcesso &= " --newline"
        ConverterArgumentosSeparados(psi, argumentosProcesso)
        psi.WorkingDirectory = Application.StartupPath
        psi.UseShellExecute = False
        psi.RedirectStandardOutput = True
        psi.RedirectStandardError = True
        psi.CreateNoWindow = True

        processoYtDlp = New Process()
        Dim proc = processoYtDlp
        proc.StartInfo = psi
        proc.EnableRaisingEvents = True

        AddHandler proc.OutputDataReceived, Sub(s, ev)
                                                If ev.Data Is Nothing Then
                                                    stdoutConcluido.TrySetResult(True)
                                                    Return
                                                End If
                                                If ev.Data IsNot Nothing Then
                                                    Dim linha As String = ev.Data.Trim() ' Remover espaços em branco no início/fim

                                                    ' --- Nova Lógica de Fases e Progresso ---

                                                    If linha.Contains("[download] Destination:") Then
                                                        Dim indiceDestino = linha.IndexOf("[download] Destination:", StringComparison.Ordinal)
                                                        Dim caminhoDestino = linha.Substring(indiceDestino + "[download] Destination:".Length).Trim().Trim(""""c)
                                                        If Path.IsPathRooted(caminhoDestino) Then
                                                            arquivosDestinoDoLink.TryAdd(caminhoDestino, 0)
                                                            If linkAtualEhLive Then liveArquivoEmGravacao = caminhoDestino
                                                        End If
                                                        estimador.Reiniciar()

                                                        ' É o início de um novo arquivo sendo baixado (áudio ou vídeo)
                                                        If currentLinkPhase = CurrentDownloadPhase.Initial Then
                                                            currentLinkPhase = CurrentDownloadPhase.DownloadingPart1
                                                            ' Me.Invoke(Sub() StatusLabel.Text = "Status: Download em progresso...")
                                                        ElseIf currentLinkPhase = CurrentDownloadPhase.DownloadingPart1 Then
                                                            currentLinkPhase = CurrentDownloadPhase.DownloadingPart2
                                                            ' Me.Invoke(Sub() StatusLabel.Text = "Status: Baixando parte 2/2...")
                                                        End If
                                                        ' Resetar a barra para o download da parte
                                                        Me.Invoke(Sub() progressBar.Value = 0)
                                                    End If

                                                    If linha.Contains("[download] Downloading item") Then
                                                        estimador.Reiniciar()
                                                        Me.Invoke(Sub()
                                                                      Dim statusText As String = linha.Replace("[download] Downloading item", "Status: Baixando item")
                                                                      StatusLabel.Text = statusText
                                                                  End Sub)
                                                        logTextBox.Invoke(Sub() logTextBox.AppendText(linha & Environment.NewLine))
                                                        Return ' Já processado
                                                    End If

                                                    If linha.Contains("[Merger] Merging formats into") Then
                                                        currentLinkPhase = CurrentDownloadPhase.Merging
                                                        Me.Invoke(Sub()
                                                                      AtualizarStatus("Status: Aguarde...")
                                                                      progressBar.Value = 95 ' Fixa em 95% ou 99% para indicar quase lá
                                                                      Me.Cursor = Cursors.WaitCursor
                                                                      txtLog.Cursor = Cursors.WaitCursor
                                                                  End Sub)
                                                        logTextBox.Invoke(Sub() logTextBox.AppendText(linha & Environment.NewLine))
                                                        Return ' Já processado
                                                    End If

                                                    If linha.Contains("[download] Download complete") Then
                                                        ' Uma parte (áudio ou vídeo) terminou.
                                                        If currentLinkPhase = CurrentDownloadPhase.DownloadingPart1 Then
                                                            Me.Invoke(Sub() progressBar.Value = 50) ' Áudio 100% (50% do total do link)
                                                        ElseIf currentLinkPhase = CurrentDownloadPhase.DownloadingPart2 Then
                                                            Me.Invoke(Sub() progressBar.Value = 90) ' Vídeo 100% (90% do total do link, resto para merge)
                                                        End If
                                                        logTextBox.Invoke(Sub() logTextBox.AppendText(linha & Environment.NewLine))
                                                        Return ' Já processado
                                                    End If

                                                    If linha.Contains("Deleting original file") Then
                                                        Return ' Ignora esta linha no log
                                                    End If

                                                    ' Tenta extrair o progresso da linha de saída
                                                    Dim match As Match = Regex.Match(linha, "\[download\]\s+(\d{1,3}(?:\.\d+)?)%")
                                                    If match.Success Then
                                                        'timerFakeProgress.Stop() ' Se for um download normal, para o fake progress
                                                        Dim percentText = match.Groups(1).Value.Replace(",", ".")
                                                        Dim percentEtapa As Integer = CInt(Math.Floor(Double.Parse(percentText, Globalization.CultureInfo.InvariantCulture)))
                                                        percentEtapa = Math.Min(percentEtapa, 100) ' Garante que não exceda 100
                                                        Dim etaTexto = estimador.Atualizar(linha, Double.Parse(percentText, Globalization.CultureInfo.InvariantCulture), relogioEta.Elapsed.TotalSeconds)
                                                        Dim linhaProgresso = Regex.Replace(linha, "\bETA\s+\S+", "ETA " & etaTexto)
                                                        If Not linha.Contains("ETA ") Then linhaProgresso &= " | ETA " & etaTexto



                                                        ' Lógica para mapear o progresso da etapa para o progresso do link completo (0-100)
                                                        Dim progressoLink As Integer = 0
                                                        Select Case currentLinkPhase
                                                            Case CurrentDownloadPhase.DownloadingPart1
                                                                ' A primeira parte representa 50% do progresso total do link
                                                                progressoLink = CInt(percentEtapa * 0.5)
                                                            Case CurrentDownloadPhase.DownloadingPart2
                                                                ' A segunda parte representa os outros 40% (50% já do áudio + 40% do vídeo = 90%)
                                                                progressoLink = 50 + CInt(percentEtapa * 0.4)
                                                            Case Else ' Se for um download de arquivo único, ou HLS
                                                                progressoLink = percentEtapa
                                                        End Select
                                                        ' AtualizarStatus("Status: Download em andamento...")
                                                        Me.Invoke(Sub()
                                                                      progressBar.Value = Math.Min(progressoLink, progressBar.Maximum)
                                                                      AtualizarStatus($"Status: Baixando {Math.Min(linksConcluidos + 1, totalLinksNaFila)} de {totalLinksNaFila} | {percentText}% | ETA: {etaTexto}")
                                                                      AtualizarNotifyIconProgresso()
                                                                      Me.Cursor = Cursors.Default
                                                                      txtLog.Cursor = Cursors.Default
                                                                      chkLegendas.Enabled = False
                                                                      CheckBoxAudio.Enabled = False
                                                                      btLimparLista.Enabled = False
                                                                  End Sub)
                                                        logTextBox.Invoke(Sub() AtualizarLinhaProgresso(logTextBox, linhaProgresso, ultimaLinhaDownload))
                                                        Return ' Linha de progresso processada
                                                    End If

                                                    ' Se for uma linha que não é de progresso mas é relevante para o log
                                                    logTextBox.Invoke(Sub() logTextBox.AppendText(linha & Environment.NewLine))

                                                End If ' End If ev.Data IsNot Nothing
                                            End Sub

        ' Handler para a saída de erro (error)
        AddHandler proc.ErrorDataReceived, Sub(s, ev)
                                               If ev.Data Is Nothing Then
                                                   stderrConcluido.TrySetResult(True)
                                                   Return
                                               End If
                                               If ev.Data IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(ev.Data) Then
                                                   If ev.Data.Contains("[download] Destination:", StringComparison.Ordinal) Then
                                                       Dim indiceDestino = ev.Data.IndexOf("[download] Destination:", StringComparison.Ordinal)
                                                       Dim caminhoDestino = ev.Data.Substring(indiceDestino + "[download] Destination:".Length).Trim().Trim(""""c)
                                                       If Path.IsPathRooted(caminhoDestino) Then
                                                           arquivosDestinoDoLink.TryAdd(caminhoDestino, 0)
                                                           If linkAtualEhLive Then liveArquivoEmGravacao = caminhoDestino
                                                       End If
                                                   End If

                                                   Dim linha = ev.Data.Trim().ToLower()
                                                   Dim linhaOriginal = ev.Data
                                                   ' O FFmpeg ignora metadados MOOV repetidos; informa apenas uma vez por captura.
                                                   If linha.StartsWith("[mov,") AndAlso linha.Contains("found duplicated moov atom.") Then
                                                       aguardandoComplementoMoov = Not linha.Contains("skipped it")
                                                       If Not avisoMoovExibido Then
                                                           avisoMoovExibido = True
                                                           logTextBox.Invoke(Sub() logTextBox.AppendText("[AVISO] Metadados MP4 repetidos foram ignorados pelo FFmpeg. Avisos iguais serão omitidos nesta captura." & Environment.NewLine))
                                                       End If
                                                       Return
                                                   End If

                                                   If aguardandoComplementoMoov AndAlso linha = "skipped it" Then
                                                       aguardandoComplementoMoov = False
                                                       Return
                                                   End If
                                                   aguardandoComplementoMoov = False
                                                   ' Termos típicos de HLS que queremos interceptar (e não registrar como erro fatal no log)
                                                   Dim termosHLS = New String() {
                    "duration:", "stream mapping:", "metadata:", "stream #", "input #", "output #", "[https @",
                    "program ", "encoder", "lavf", "variant_bitrate", "timed_id3", "[hls @", "[mpegts @", "size=", "chunklist", "skip ('#ext", "skipping", "press [q]", "[tls @", "io error"}

                                                   If termosHLS.Any(Function(p) linha.StartsWith(p) OrElse linha.Contains(p)) Then
                                                       Me.Invoke(Sub()
                                                                     Try
                                                                         Dim tamanho = ObterTamanhoDaPasta(pastaDestino)
                                                                         Dim tempoGravacao = DateTime.Now - inicioHLS
                                                                         Dim tempoTexto = $"{tempoGravacao.Minutes:D2}:{tempoGravacao.Seconds:D2}"

                                                                         If Not String.IsNullOrEmpty(ultimaLinhaHLS) Then
                                                                             txtLog.Text = txtLog.Text.Replace(ultimaLinhaHLS, "")
                                                                         End If

                                                                         ultimaLinhaHLS = $"📡 Gravando stream HLS... Tempo: {tempoTexto} | Tamanho: {tamanho}" & Environment.NewLine

                                                                         txtLog.AppendText(ultimaLinhaHLS)
                                                                         AtualizarStatus($"Status: Gravando stream HLS... Tempo: {tempoTexto} | Tamanho atual: {tamanho}")
                                                                         NotifyIcon1.Text = $"Gravando stream HLS... Tempo: {tempoTexto} | Tamanho atual: {tamanho}"
                                                                     Catch ex As Exception
                                                                         Debug.WriteLine($"[ERRO ao atualizar log HLS] {ex.Message}")
                                                                     End Try
                                                                 End Sub)
                                                       Return
                                                   End If

                                                   Dim palavrasErroCritico = New String() {"error", "failed", "unable", "not found", "forbidden"}
                                                   If palavrasErroCritico.Any(Function(p) linha.Contains(p)) Then
                                                       hasErrors = True
                                                       Me.Invoke(Sub()
                                                                     txtLog.AppendText("[ERRO] " & linhaOriginal & Environment.NewLine)
                                                                     AtualizarStatus("Status: Erro!")
                                                                 End Sub)
                                                       Return
                                                   End If

                                                   ' Lógica para ignorar logs de legendas durante a listagem
                                                   If ev.Data.Contains("Available automatic captions for") OrElse ev.Data.Contains("Available subtitles for") Then
                                                       ignorandoListaLegendas = True
                                                   End If

                                                   If ignorandoListaLegendas AndAlso ev.Data.Contains("format:") Then
                                                       ' Fim da lista de legendas (ou parte dela)
                                                       ignorandoListaLegendas = False
                                                       Return
                                                   End If

                                                   If Not ignorandoListaLegendas Then
                                                       Me.Invoke(Sub() txtLog.AppendText(ev.Data & Environment.NewLine))
                                                   End If



                                               End If

                                           End Sub

        Try
            If canceladoPeloUsuario Then Return False
            inicioHLS = DateTime.Now
            proc.Start()
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()
            ' Aguarda também o escoamento dos eventos de stdout/stderr antes de decidir o resultado.
            Await proc.WaitForExitAsync()
            Await Task.WhenAll(stdoutConcluido.Task, stderrConcluido.Task)
            exitCode = proc.ExitCode
            Return Not canceladoPeloUsuario AndAlso Not hasErrors AndAlso exitCode = 0
        Catch ex As Exception
            logTextBox.AppendText("[FALHA] Não foi possível executar o download: " & ex.Message & Environment.NewLine)
            AtualizarStatus("Status: Falha no processo...")
            Return False
        Finally
            If canceladoPeloUsuario AndAlso Not encerrandoLive Then
                For Each destino In arquivosDestinoDoLink.Keys
                    Try
                        Dim pasta = Path.GetDirectoryName(destino)
                        Dim nomeDestino = Path.GetFileName(destino)
                        Dim parciais As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
                            destino & ".part",
                            destino & ".ytdl"
                        }
                        If Directory.Exists(pasta) Then
                            For Each parcial In Directory.GetFiles(pasta, nomeDestino & ".part*")
                                parciais.Add(parcial)
                            Next
                        End If
                        For Each parcial In parciais
                            If File.Exists(parcial) Then File.Delete(parcial)
                        Next
                    Catch ex As Exception
                        logTextBox.AppendText($"[AVISO] Não foi possível remover todos os parciais de {Path.GetFileName(destino)}: {ex.Message}{Environment.NewLine}")
                    End Try
                Next
                logTextBox.AppendText("[Cancelamento] Arquivos parciais do link atual removidos." & Environment.NewLine)
            ElseIf encerrandoLive Then
                logTextBox.AppendText("[Live] Preservando o fluxo MPEG-TS gravado para finalizar e salvar a captura." & Environment.NewLine)
            End If
            ultimaLinhaHLS = ""
            If Object.ReferenceEquals(processoYtDlp, proc) Then processoYtDlp = Nothing
            proc.Dispose()
        End Try
    End Function

    Private Function ObterTamanhoDaPasta(pasta As String) As String
        Try
            Dim tamanhoTotal As Long = 0
            If linkAtualEhLive AndAlso Not String.IsNullOrWhiteSpace(liveArquivoEmGravacao) AndAlso File.Exists(liveArquivoEmGravacao) Then
                tamanhoTotal = New FileInfo(liveArquivoEmGravacao).Length
            Else
                For Each arquivo In Directory.GetFiles(pasta, "*.part", SearchOption.TopDirectoryOnly)
                    tamanhoTotal += New FileInfo(arquivo).Length
                Next
            End If

            Return (tamanhoTotal / 1024 / 1024).ToString("0.00") & " MB"
        Catch
            Return "?"
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
            txtLog.Visible = False
            progressBarDownload.Location = New Point(12, 224)
            Height = 335
        Else
            txtLog.Visible = True
            txtLog.Location = New Point(12, 222)
            progressBarDownload.Location = New Point(12, 407)
            Height = 520
        End If
        'lstLink.EnsureVisible(lstLink.Items.Count - 1)
    End Sub
    Private Sub AlterarPastaDestinoToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles AlterarPastaDestinoToolStripMenuItem.Click
        If downloadEmAndamento Then Return
        Dim folderBrowser As New FolderBrowserDialog With {
            .Description = "Selecione a pasta de destino para os downloads:"
        }
        If folderBrowser.ShowDialog() = DialogResult.OK Then
            My.Settings.destFolder = folderBrowser.SelectedPath
            My.Settings.Save()
            My.Settings.Upgrade()
            txtLog.AppendText($"🗂️ Pasta de destino alterada para: {My.Settings.destFolder}" & Environment.NewLine)
            Application.Restart()
            'Environment.Exit(0)
        End If

    End Sub

    Private Sub btCancelar_Click(sender As Object, e As EventArgs) Handles btCancelar.Click
        If canceladoPeloUsuario Then Return
        canceladoPeloUsuario = True
        btCancelar.Enabled = False
        timerFakeProgress.Stop()
        encerrandoLive = linkAtualEhLive OrElse liveCapturas.Count > 0
        AtualizarStatus(If(encerrandoLive, "Status: Encerrando e salvando a live...", "Status: Cancelando download..."))

        For Each captura In liveCapturas.Values.ToArray()
            captura.StopRequested = True
            AtualizarStatusLink(captura.Link, "Encerrando")
            Try
                If captura.Process IsNot Nothing AndAlso Not captura.Process.HasExited Then captura.Process.Kill(entireProcessTree:=True)
            Catch
            End Try
        Next

        ' O executor aguarda a saída e remove apenas os parciais identificados para este link.
        Dim proc = processoYtDlp
        Try
            If proc IsNot Nothing AndAlso Not proc.HasExited Then
                proc.Kill(entireProcessTree:=True)
            End If
        Catch ex As InvalidOperationException
            ' O processo pode ter terminado entre a consulta e o pedido de cancelamento.
        Catch ex As Exception
            canceladoPeloUsuario = False
            btCancelar.Enabled = True
            txtLog.AppendText($"[ERRO ao cancelar] {ex.Message}{Environment.NewLine}")
            AtualizarStatus("Status: Não foi possível cancelar. Tente novamente.")
            Return
        End Try
        If encerrandoLive Then
            txtLog.AppendText("[Live] Encerrando a captura para preservar o vídeo no formato MPEG-TS." & Environment.NewLine)
        Else
            txtLog.AppendText("[Cancelamento] Aguardando o encerramento para remover os parciais deste link." & Environment.NewLine)
        End If
    End Sub

    Private Sub ImportarCookiesPrivadosToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ImportarCookiesPrivadosToolStripMenuItem.Click
        If downloadEmAndamento Then Return
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
                    File.Delete(cookiesFilePath) ' Remove o arquivo antigo, se existir
                End If
                FileCopy(cookiesPath, cookiesFilePath)
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
        If downloadEmAndamento OrElse verificacaoAtualizacaoEmAndamento Then Return
        Await VerificarAtualizacaoYTDLP()
    End Sub
    ' Minimizar para o Tray
    Private Sub MinimizarParaTray()
        Me.Hide()
        Me.ShowInTaskbar = True
        NotifyIcon1.Visible = True
        NotifyIcon1.BalloonTipTitle = " "
        NotifyIcon1.BalloonTipText = "O programa está em execução em segundo plano."
        NotifyIcon1.ShowBalloonTip(1000)
    End Sub

    Private Sub Form1_Resize(sender As Object, e As EventArgs) Handles MyBase.Resize
        If Me.WindowState = FormWindowState.Minimized Then
            MinimizarParaTray()
        End If
    End Sub
    Private Sub AtualizarNotifyIconProgresso()
        Dim porcentagem As Integer = 0
        If progressBarDownload.Maximum > 0 Then
            porcentagem = CInt((progressBarDownload.Value / progressBarDownload.Maximum) * 100)
        End If

        Dim texto = $"Download: {porcentagem}% completo {vbCrLf}{StatusLabel.Text}"
        NotifyIcon1.Text = If(texto.Length > 63, texto.Substring(0, 63), texto)
    End Sub

    Public Function ListViewContains(ByVal listView As ListView, ByVal linkProcurado As String) As Boolean
        For Each item As ListViewItem In listView.Items
            '  MsgBox($"Verificando item: {item.Tag.ToString} contra {linkProcurado}")

            If item.Tag.ToString.Equals(linkProcurado, StringComparison.OrdinalIgnoreCase) Then
                Return True ' Link encontrado
            End If
        Next
        Return False ' Link não encontrado
    End Function

    Private Function LinkPareceVideo(link As String) As Boolean
        Dim uri As Uri = Nothing
        If Not Uri.TryCreate(link, UriKind.Absolute, uri) OrElse
           (uri.Scheme <> Uri.UriSchemeHttp AndAlso uri.Scheme <> Uri.UriSchemeHttps) Then
            Return False
        End If

        Dim caminho = uri.AbsolutePath.ToLowerInvariant()
        Dim extensoesDeVideo = {".mp4", ".m4v", ".mkv", ".webm", ".mov", ".avi", ".flv", ".ts", ".m3u8", ".mpd"}
        If extensoesDeVideo.Any(Function(extensao) caminho.EndsWith(extensao, StringComparison.OrdinalIgnoreCase)) Then
            Return True
        End If

        ' O monitor da área de transferência deve ignorar páginas comuns e links de arquivos.
        ' Esses domínios são usados por plataformas de vídeo reconhecidas pelo yt-dlp.
        Dim dominiosDeVideo = {
            "youtube.com", "youtu.be", "youtube-nocookie.com", "googlevideo.com",
            "twitch.tv", "twitchcdn.net", "chaturbate.com", "vimeo.com", "dailymotion.com",
            "tiktok.com", "tiktokcdn.com", "instagram.com", "facebook.com", "fb.watch",
            "twitter.com", "x.com", "reddit.com", "redd.it", "kick.com", "rumble.com",
            "bilibili.com", "streamable.com", "loom.com", "videopress.com", "clips.twitch.tv"
        }

        Dim host = uri.DnsSafeHost.TrimEnd("."c)
        Return dominiosDeVideo.Any(Function(dominio) _
            host.Equals(dominio, StringComparison.OrdinalIgnoreCase) OrElse
            host.EndsWith("." & dominio, StringComparison.OrdinalIgnoreCase))
    End Function

    Private Async Sub TimerClipboard_Tick(sender As Object, e As EventArgs) Handles TimerClipboard.Tick
        If downloadEmAndamento Then Return
        Try
            If Clipboard.ContainsText() Then
                Dim linkDetected As String = Clipboard.GetText().Trim()
                If Not linkDetected.Equals(ultimoLinkDetectado, StringComparison.Ordinal) Then
                    ' Memoriza qualquer mudança na área de transferência, inclusive texto sem vídeo.
                    ' Assim, excluir o item não faz o mesmo conteúdo copiado gerar outra pergunta.
                    ultimoLinkDetectado = linkDetected

                    If LinkPareceVideo(linkDetected) AndAlso ListViewContains(lstLink, linkDetected) = False Then
                        Dim resposta = MessageBox.Show($"Link detectado na área de transferência:{Environment.NewLine}{linkDetected}{Environment.NewLine}{Environment.NewLine}Deseja adicionar à lista de downloads?", "Novo Link Detectado", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

                        If resposta = DialogResult.Yes Then
                            Await addLink(linkDetected)
                        End If
                    End If
                End If
            End If
        Catch ex As Exception
            txtLog.AppendText($"[ERRO Monitor Clipboard] {ex.Message}{Environment.NewLine}")
        End Try
    End Sub
    Private Sub CheckBoxAudio_CheckedChanged(sender As Object, e As EventArgs) Handles CheckBoxAudio.CheckedChanged
        If CheckBoxAudio.Checked Then
            chkLegendas.Enabled = False
            chkLegendas.Checked = False
        Else
            chkLegendas.Enabled = True
        End If
    End Sub
    Private Sub chkLegendas_CheckedChanged(sender As Object, e As EventArgs) Handles chkLegendas.CheckedChanged
        If chkLegendas.Checked Then
            CheckBoxAudio.Enabled = False
            CheckBoxAudio.Checked = False
        Else
            CheckBoxAudio.Enabled = True
        End If
    End Sub
    Private Sub NotifyIcon1_MouseClick(sender As Object, e As MouseEventArgs) Handles NotifyIcon1.MouseClick
        Me.Show()
        Me.WindowState = FormWindowState.Normal
        Me.ShowInTaskbar = True
        NotifyIcon1.Visible = False
    End Sub
End Class
