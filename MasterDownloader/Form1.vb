Imports System.Diagnostics
Imports System.IO
Imports System.Runtime.InteropServices.JavaScript.JSType
Imports System.Security.Policy
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading.Tasks
Imports System.Windows.Forms.LinkLabel

Public Class Form1
    Dim downloadFilePath As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PbPb Downloader", "download.txt")
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
    Private ultimoLinkDetectado As String = ""
    Private currentDownloadLink As String = ""

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

    Private Async Function addLink(ByVal link As String) As Task

        If link <> "" Then
            If Not File.Exists(downloadFilePath) Then
                MessageBox.Show("arquivo não encontrado.")
                Return
            End If
            StatusLabel.Text = "Status: Adicionando link..."
            Application.DoEvents()
            btnExecutar.Enabled = False
            Me.Cursor = Cursors.WaitCursor
            File.AppendAllText(downloadFilePath, link & Environment.NewLine)
            txtUrl.Clear()
        End If

        Dim links = File.ReadAllLines(downloadFilePath).Where(Function(l) Not String.IsNullOrWhiteSpace(l)).ToList()
        If links.Count = 0 Then
            txtLog.AppendText("⚠️ Nenhum link encontrado no arquivo." & Environment.NewLine)
            btnExecutar.Enabled = True
            Return
        End If

        Try
            Dim videoData = Await ContarVideosNaPlaylist(link)
            AdicionarTituloNaListView(UnescapeUnicode(videoData.Item2), link)
            ' lstLink.Items.Add(videoData.Item2)
            txtLog.AppendText($"📺 Link contém {videoData.Item1} vídeos." & Environment.NewLine)
            StatusLabel.Text = $"Status: {videoData.Item1} vídeos encontrados"
            btnExecutar.Enabled = True
        Catch ex As Exception
            StatusLabel.Text = "Status: Nenhum video encontrado."
            txtLog.AppendText("❌ Falha ao contar vídeos: " & ex.Message & Environment.NewLine)
        End Try
        Me.Cursor = Cursors.Default
    End Function
    Private Async Sub BtnAdicionar_Click(sender As Object, e As EventArgs) Handles btnAdicionar.Click
        Await addLink(txtUrl.Text.Trim())
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
            AtualizarStatus("Status: Pronto...")
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
        TimerClipboard.Start()

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

    Private Async Function CarregarLegendasDisponiveis(link As String) As Task
        Dim psi As New ProcessStartInfo With {
        .FileName = Path.Combine(Application.StartupPath, "app", "yt-dlp.exe"),
        .Arguments = $"--list-subs ""{link}"" --cookies ""cookies.txt"" --no-warnings",
        .UseShellExecute = False,
        .RedirectStandardOutput = True,
        .RedirectStandardError = True,
        .CreateNoWindow = True,
        .RedirectStandardInput = True
    }

        Using proc As Process = Process.Start(psi)
            Dim output As String = Await proc.StandardOutput.ReadToEndAsync()
            Dim errors As String = Await proc.StandardError.ReadToEndAsync()
            proc.WaitForExit()
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
        End Using
    End Function
    Private Async Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        Dim txtDownload As String = Path.GetDirectoryName(downloadFilePath)
        If Not Directory.Exists(txtDownload) Then
            Directory.CreateDirectory(txtDownload)
        End If
        Dim caminhoDestino = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "PbPb Downloader",
    "downloaded")

        If String.IsNullOrWhiteSpace(My.Settings.destFolder) Then
            My.Settings.destFolder = caminhoDestino
            My.Settings.Save()
        End If

        NotifyIcon1.Text = "PbPb Downloader"
        progressBarDownload.Location = New Point(12, 224)
        Me.Height = 335
        AddHandler timerFakeProgress.Tick, AddressOf timerFakeProgress_Tick
        lstLink.Columns.Add("Título", 400)
        lstLink.Columns.Add("Status", 50)
        lstLink.Columns.Add("Action", 40)

        If File.Exists(downloadFilePath) Then
            AtualizarStatus("Status: Processando links...")
            btnExecutar.Enabled = False
            Dim links = File.ReadAllLines(downloadFilePath)

            For Each link In links
                Dim videoData = Await ContarVideosNaPlaylist(link)
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

    Private Function onlyAudio(link)
        Dim argsAudio As New StringBuilder()
        argsAudio.Append("--extract-audio --audio-format mp3 ")
        argsAudio.Append("--format bestaudio/best ")
        argsAudio.Append($"--output ""{My.Settings.destFolder}\%(title)s.%(ext)s"" ""{link}"" ")
        argsAudio.Append("--ignore-errors ")
        argsAudio.Append("--cookies ""cookies.txt"" ")
        ' argsAudio.Append("--cookies-from-browser chrome ")
        argsAudio.Append("--no-warnings ")

        Return argsAudio

    End Function

    Public Function argsLegend(args As String)
        Dim argsLeg As New StringBuilder()
        argsLeg.Append(args)
        Return argsLeg
    End Function

    Private Sub cleanFiles()
        Dim extensoesPermitidas = New String() {".mp4", ".mp3"}
        Dim totalDeletados As Integer = 0

        Try
            Dim arquivos = Directory.GetFiles(pastaDestino, "*.*", SearchOption.TopDirectoryOnly)

            For Each arquivo In arquivos
                Dim ext As String = Path.GetExtension(arquivo).ToLower()
                Dim nomeArquivo As String = Path.GetFileName(arquivo).ToLower()

                Dim deveExcluir As Boolean = False

                ' Excluir se a extensão não for mp4 nem mp3
                If Not extensoesPermitidas.Contains(ext) Then
                    deveExcluir = True
                End If

                ' Excluir também arquivos com ".fXXX.mp4" no nome (ex: .f135.mp4, .f136.mp4, etc)
                If ext = ".mp4" AndAlso Regex.IsMatch(nomeArquivo, "\.f\d{3,}\.mp4$") Then
                    deveExcluir = True
                End If

                ' Excluir também qualquer .part, .json, .vtt, .temp ou outros conhecidos
                If nomeArquivo.EndsWith(".part") OrElse
               nomeArquivo.EndsWith(".json") OrElse
               nomeArquivo.EndsWith(".vtt") OrElse
               nomeArquivo.EndsWith(".temp") OrElse
               nomeArquivo.EndsWith(".m4a") OrElse
               nomeArquivo.EndsWith(".webm") Then
                    deveExcluir = True
                End If

                If deveExcluir Then
                    Try
                        DeleteFileSafe(arquivo)
                        totalDeletados += 1
                        '  txtLog.AppendText($"🧹 Arquivo deletado: {Path.GetFileName(arquivo)}" & Environment.NewLine)
                    Catch ex As Exception
                        txtLog.AppendText($"[ERRO ao deletar] {Path.GetFileName(arquivo)} - {ex.Message}" & Environment.NewLine)
                    End Try
                End If
            Next

            ' txtLog.AppendText($"✅ Limpeza finalizada. Total de arquivos deletados: {totalDeletados}" & Environment.NewLine)

        Catch ex As Exception
            txtLog.AppendText($"[ERRO durante limpeza] {ex.Message}" & Environment.NewLine)
        End Try
    End Sub

    Private Async Sub BtnExecutar_Click(sender As Object, e As EventArgs) Handles btnExecutar.Click
        btnExecutar.Enabled = False
        btCancelar.Enabled = True
        'timerFakeProgress.Start()
        progressBarDownload.Value = 0
        StatusLabel.Text = "Status: Iniciando..."
        Me.Cursor = Cursors.WaitCursor
        txtLog.Clear()
        Application.DoEvents()


        Dim successOverall As Boolean = True ' Para rastrear se todos os downloads tiveram sucesso
        Dim linksList As New List(Of String)()
        If File.Exists(downloadFilePath) Then
            linksList = File.ReadAllLines(downloadFilePath).Where(Function(l) Not String.IsNullOrWhiteSpace(l)).ToList()
        End If

        If linksList.Count = 0 Then
            MessageBox.Show("Nenhum link válido encontrado.")
            txtLog.AppendText(Environment.NewLine & "❌ Nenhum link válido encontrado." & Environment.NewLine)
            btnExecutar.Enabled = True
            btCancelar.Enabled = False
            timerFakeProgress.Stop()
            Me.Cursor = Cursors.Default
            StatusLabel.Text = "Status: Pronto"
            Exit Sub
        End If

        linksConcluidos = 1
        canceladoPeloUsuario = False


        ' AQUI MUDAMOS A LÓGICA DO PROGRESSBAR.MAXIMUM
        ' Se cada link pode ter 2 etapas (audio + video + merging),
        ' e para cada etapa você quer 100%, então o máximo é linksList.Count * 100
        ' ou linksList.Count * 200, se você quiser dividir o progresso de download entre as duas partes
        ' Para simplificar, vou manter 100% por link e gerenciar internamente.
        progressBarDownload.Maximum = 100 ' O progresso será por link, de 0 a 100%

        Try
            For Each link In linksList
                If canceladoPeloUsuario Then Exit For
                currentDownloadLink = link
                Dim linkOriginal As String = link ' Mantém o link original para referência

                ' Resetar a barra para cada link
                Me.Invoke(Sub()
                              progressBarDownload.Value = 0
                              If IsPlaylist(link) Then
                                  StatusLabel.Text = $"Status: Baixando playlist..."
                              Else
                                  StatusLabel.Text = $"Status: Baixando {linksConcluidos} de {linksList.Count}..."
                              End If
                              ' StatusLabel.Text = $"Status: Baixando {linksConcluidos} de {linksList.Count}..."
                          End Sub)
                Application.DoEvents() ' Processa eventos para atualizar a UI

                Dim args As New StringBuilder()
                ' Definindo argumentos base para yt-dlp
                args.Append($"--output ""{My.Settings.destFolder}\%(title)s.%(ext)s"" ""{link}"" ")
                args.Append("--cookies ""cookies.txt"" ")
                args.Append("--no-warnings ")
                args.Append("--progress --newline --no-mtime ") ' Manter essas para o parser

                If IsHLS(link) Then
                    timerFakeProgress.Start() ' Seu timer para HLS
                    Me.Cursor = Cursors.Default
                    chkLegendas.Enabled = False
                    CheckBoxAudio.Enabled = False
                    args.Append("--format best --downloader ffmpeg --buffer-size 1M --ignore-errors ")
                    ' Para HLS, o yt-dlp usa ffmpeg e a saída de progresso é diferente
                    ' Você pode precisar de uma lógica de parsing de HLS mais específica em ExecutarProcessoAsync
                    ' ou confiar no seu timerFakeProgress para preencher a barra.
                    If Await ExecutarProcessoAsync(txtLog, progressBarDownload, args.ToString()) Then
                        MarcarItemComoOK(linkOriginal)
                    Else
                        successOverall = False
                    End If
                    Continue For ' Próximo link
                End If

                ' Lógica para Playlists e Vídeos/Áudio individuais
                If IsPlaylist(link) Then
                    If CheckBoxAudio.Checked Then
                        args = onlyAudio(link) ' Já inclui o --output e outras configs
                    Else
                        args.Append("--extractor-args ""youtubetab:skip=authcheck"" ")
                        args.Append("--format bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best ") ' Tenta mesclar em MP4
                    End If
                Else ' Single Video
                    If CheckBoxAudio.Checked Then
                        args = onlyAudio(link)
                    Else
                        args.Append("--extractor-args ""youtubetab:skip=authcheck"" ")
                        args.Append("--format bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best --no-playlist ")
                        args.Append("--merge-output-format mp4 ") ' Garante saída MP4 se houver fusão
                    End If
                End If

                ' Lógica de Legendas
                If chkLegendas.Checked AndAlso Not CheckBoxAudio.Checked Then ' Legendas só fazem sentido para vídeo
                    Await CarregarLegendasDisponiveis(link)
                    Dim resultado As DialogResult = FormLegendas.ShowDialog()
                    If resultado = DialogResult.OK AndAlso Not String.IsNullOrEmpty(FormLegendas.args) Then
                        args.Append(" " & FormLegendas.args & " ")
                    End If
                End If

                ' Agora executamos o processo para o link atual
                If canceladoPeloUsuario Then Exit For ' Verifica cancelamento antes de executar
                If Await ExecutarProcessoAsync(txtLog, progressBarDownload, args.ToString()) Then
                    MarcarItemComoOK(linkOriginal)
                    linksConcluidos += 1
                Else
                    successOverall = False
                End If
            Next

            ' --- Finalização ---
            If Not canceladoPeloUsuario And successOverall Then
                txtLog.AppendText(Environment.NewLine & "✅ Todos os arquivos baixados com sucesso!" & Environment.NewLine)
                OpenFolder()
                StatusLabel.Text = "Status: Pronto"
                Application.DoEvents()
                Me.Cursor = Cursors.Default
                cleanFiles()
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
            Application.DoEvents()
            NotifyIcon1.BalloonTipTitle = "❌ Download Falhou"
            NotifyIcon1.BalloonTipText = $"Ocorreu uma falha durante o download."
            NotifyIcon1.ShowBalloonTip(2000)
            successOverall = False
        Finally
            canceladoPeloUsuario = False
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

            If successOverall AndAlso Not canceladoPeloUsuario Then
                NotifyIcon1.BalloonTipTitle = "✅ Download Concluído"
                NotifyIcon1.BalloonTipText = $"Todos os arquivos foram baixados com sucesso."
                NotifyIcon1.ShowBalloonTip(2000)
            ElseIf canceladoPeloUsuario Then
                NotifyIcon1.BalloonTipTitle = "⛔ Download Cancelado"
                NotifyIcon1.BalloonTipText = $"O processo foi cancelado pelo usuário."
                NotifyIcon1.ShowBalloonTip(2000)
            End If
            NotifyIcon1.Text = "PbPb Downloader"
        End Try
    End Sub

    ' --- ExecutarProcessoAsync Modificado ---
    Public Async Function ExecutarProcessoAsync(ByVal logTextBox As TextBox, ByVal progressBar As ProgressBar, ByVal argumentos As String) As Task(Of Boolean)

        Dim tcs As New TaskCompletionSource(Of Boolean)()
        Dim hasErrors As Boolean = False
        Dim exitCode As Integer = -1
        Dim ignorandoListaLegendas As Boolean = False ' Variável para controlar o estado de ignorar logs de legenda
        currentLinkPhase = CurrentDownloadPhase.Initial ' Resetar a fase para cada novo link

        Dim psi As New ProcessStartInfo With {
            .FileName = IO.Path.Combine(Application.StartupPath, "app", "yt-dlp.exe") ' Caminho para yt-dlp
        }

        ' Adicionar --progress e --newline se já não estiver nos argumentos
        If Not argumentos.Contains("--progress") Then psi.Arguments &= " --progress"
        If Not argumentos.Contains("--newline") Then psi.Arguments &= " --newline"
        psi.Arguments &= " " & argumentos ' Adiciona os argumentos específicos do link
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
                                                If ev.Data IsNot Nothing Then
                                                    Dim linha As String = ev.Data.Trim() ' Remover espaços em branco no início/fim

                                                    ' --- Nova Lógica de Fases e Progresso ---

                                                    If linha.Contains("[download] Destination:") Then

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

                                                        Me.Invoke(Sub()
                                                                      progressBar.Value = Math.Min(progressoLink, progressBar.Maximum)
                                                                      AtualizarNotifyIconProgresso()
                                                                  End Sub)

                                                        ' AtualizarStatus("Status: Download em andamento...")
                                                        Me.Invoke(Sub()
                                                                      Me.Cursor = Cursors.Default
                                                                      txtLog.Cursor = Cursors.Default
                                                                      chkLegendas.Enabled = False
                                                                      CheckBoxAudio.Enabled = False
                                                                      btLimparLista.Enabled = False
                                                                  End Sub)
                                                        logTextBox.Invoke(Sub() logTextBox.AppendText(linha & Environment.NewLine)) ' Loga a linha de progresso
                                                        Return ' Linha de progresso processada
                                                    End If

                                                    ' Se for uma linha que não é de progresso mas é relevante para o log
                                                    logTextBox.Invoke(Sub() logTextBox.AppendText(linha & Environment.NewLine))

                                                End If ' End If ev.Data IsNot Nothing
                                            End Sub

        ' Handler para a saída de erro (error)
        AddHandler proc.ErrorDataReceived, Sub(s, ev)
                                               If ev.Data IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(ev.Data) Then
                                                   Dim linha = ev.Data.Trim().ToLower()
                                                   Dim linhaOriginal = ev.Data
                                                   ' Termos típicos de HLS que queremos interceptar (e não registrar como erro fatal no log)
                                                   Dim termosHLS = New String() {
                    "duration:", "stream mapping:", "metadata:", "stream #", "input #", "output #", "[https @",
                    "program ", "encoder", "lavf", "variant_bitrate", "timed_id3", "[hls @", "[mpegts @", "size=", "chunklist", "skip ('#ext", "skipping", "press [q]", "[tls @", "io error"}

                                                   If termosHLS.Any(Function(p) linha.StartsWith(p) OrElse linha.Contains(p)) Then
                                                       Me.Invoke(Sub()
                                                                     Try
                                                                         Dim tamanho = ObterTamanhoDaPasta(My.Settings.destFolder)
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

                                                   Me.Invoke(Sub()
                                                                 txtLog.AppendText(linhaOriginal & Environment.NewLine)
                                                             End Sub)

                                               End If

                                           End Sub

        ' Handler para quando o processo for finalizado
        AddHandler proc.Exited, Sub(s, ev)
                                    Try
                                        exitCode = proc.ExitCode
                                    Catch ex As Exception
                                        exitCode = -1 ' Em caso de erro para pegar o ExitCode
                                    End Try
                                    ultimaLinhaHLS = "" ' Reseta para o próximo HLS
                                    ' Define o resultado da tarefa para indicar que o processo terminou
                                    tcs.TrySetResult(Not hasErrors AndAlso exitCode = 0) ' Indica sucesso apenas se não houver erros e exit code for 0
                                    Me.Invoke(Sub()
                                                  ' Resetar cursores e habilitar controles ao finalizar o processo do link
                                                  Me.Cursor = Cursors.Default
                                                  txtLog.Cursor = Cursors.Default
                                                  chkLegendas.Enabled = True
                                                  CheckBoxAudio.Enabled = True
                                                  btLimparLista.Enabled = True
                                                  ' Atualizar a barra para 100% para o link atual
                                                  progressBar.Value = progressBar.Maximum
                                              End Sub)
                                End Sub

        Try
            inicioHLS = DateTime.Now ' Inicia o contador para HLS
            proc.Start()
            proc.BeginOutputReadLine()
            proc.BeginErrorReadLine()

        Catch ex As Exception
            logTextBox.Invoke(Sub() logTextBox.AppendText("[FALHA CRÍTICA] Não foi possível iniciar o processo: " & ex.Message & Environment.NewLine))
            tcs.TrySetResult(False) ' Indica falha
            AtualizarStatus("Status: Falha no processo...")
            Application.DoEvents()
        End Try

        Return Await tcs.Task ' Aguarda a conclusão da tarefa
    End Function

    Private Sub DeleteFileSafe(caminho As String)
        Dim tentativas As Integer = 0
        While tentativas < 5
            Try
                If File.Exists(caminho) Then
                    File.Delete(caminho)
                End If
                Exit While ' Sucesso, sai do loop
            Catch ex As IOException
                tentativas += 1
                Threading.Thread.Sleep(500) ' Espera meio segundo antes de tentar de novo
            End Try
        End While
    End Sub

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
        Dim folderBrowser As New FolderBrowserDialog With {
            .Description = "Selecione a pasta de destino para os downloads:"
        }
        If folderBrowser.ShowDialog() = DialogResult.OK Then
            My.Settings.destFolder = folderBrowser.SelectedPath
            My.Settings.Save()
            My.Settings.Upgrade()
            txtLog.AppendText($"🗂️ Pasta de destino alterada para: {My.Settings.destFolder}" & Environment.NewLine)
        End If

    End Sub

    Private Sub btCancelar_Click(sender As Object, e As EventArgs) Handles btCancelar.Click
        canceladoPeloUsuario = True

        Task.Run(Sub()
                     Try
                         Me.Invoke(Sub()
                                       StatusLabel.Text = "Status: Cancelando download..."
                                       'timerFakeProgress.Stop()
                                       Me.Cursor = Cursors.Default
                                   End Sub)

                         ' Tenta matar o processo yt-dlp
                         If processoYtDlp IsNot Nothing AndAlso Not processoYtDlp.HasExited Then
                             Try
                                 processoYtDlp.Kill(entireProcessTree:=True)
                                 processoYtDlp.WaitForExit(3000)
                                 processoYtDlp.Dispose()
                                 processoYtDlp = Nothing
                             Catch ex As Exception
                                 Debug.WriteLine($"{ex.Message}" & Environment.NewLine)
                             End Try

                         End If

                         If Not IsHLS(currentDownloadLink) Then
                             cleanFiles()
                         Else
                             ' Limpeza de arquivos .part
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

                                 End If
                             Next

                             Me.Invoke(Sub()
                                           NotifyIcon1.BalloonTipTitle = "✅ Streaming Concluído"
                                           NotifyIcon1.BalloonTipText = "O streaming terminou de ser capturado."
                                           NotifyIcon1.ShowBalloonTip(2000)
                                           StatusLabel.Text = "Status: O streaming terminou de ser capturado."
                                           btnExecutar.Enabled = True
                                           btCancelar.Enabled = False
                                           Me.Cursor = Cursors.Default
                                           progressBarDownload.Value = 0
                                       End Sub)

                             cleanFiles()
                             OpenFolder()
                             Exit Sub
                         End If

                         ' Limpa barra de progresso e atualiza interface
                         Me.Invoke(Sub()
                                       progressBarDownload.Value = 0
                                       StatusLabel.Text = "Status: Download cancelado pelo usuário."
                                       btnExecutar.Enabled = True
                                       btCancelar.Enabled = False
                                       Me.Cursor = Cursors.Default
                                   End Sub)

                         ' Feedback final ao usuário
                         Me.Invoke(Sub()
                                       NotifyIcon1.BalloonTipTitle = "⛔ Download Cancelado"
                                       NotifyIcon1.BalloonTipText = "O processo de download foi interrompido."
                                       NotifyIcon1.ShowBalloonTip(2000)
                                   End Sub)

                     Catch ex As Exception
                         Me.Invoke(Sub()
                                       txtLog.AppendText($"[ERRO inesperado no Cancelar] {ex.Message}" & Environment.NewLine)
                                       StatusLabel.Text = "Status: Erro ao cancelar."
                                       btnExecutar.Enabled = True
                                       btCancelar.Enabled = False
                                       Me.Cursor = Cursors.Default
                                   End Sub)
                     End Try
                 End Sub)
    End Sub


    'Private Sub btCancelar_Click(sender As Object, e As EventArgs) Handles btCancelar.Click
    '    canceladoPeloUsuario = True

    '    Task.Run(Sub()
    '                 If processoYtDlp IsNot Nothing AndAlso Not processoYtDlp.HasExited Then
    '                     Try
    '                         processoYtDlp.Kill(entireProcessTree:=True)
    '                         processoYtDlp.WaitForExit(3000)
    '                         processoYtDlp.Dispose()
    '                         processoYtDlp = Nothing

    '                         Dim arquivosPart = Directory.GetFiles(pastaDestino, "*.part", SearchOption.TopDirectoryOnly)
    '                         Dim arquivosVideo = arquivosPart.Where(Function(f) f.EndsWith(".mp4.part") OrElse f.EndsWith(".webm.part")).ToList()
    '                         Dim arquivosAudio = arquivosPart.Where(Function(f) f.EndsWith(".m4a.part") OrElse (f.EndsWith(".webm.part") AndAlso Not f.EndsWith(".mp4.part"))).ToList()

    '                         ' 1. Renomear .part para o nome final
    '                         For Each arquivo In arquivosPart
    '                             Dim novoNome = Path.Combine(pastaDestino, Path.GetFileNameWithoutExtension(arquivo))
    '                             Try
    '                                 File.Move(arquivo, novoNome)
    '                             Catch ex As Exception
    '                                 Me.Invoke(Sub() txtLog.AppendText($"[ERRO ao renomear {Path.GetFileName(arquivo)}] {ex.Message}" & Environment.NewLine))
    '                             End Try
    '                         Next

    '                         ' 2. Atualizar listas agora sem ".part"
    '                         arquivosVideo = Directory.GetFiles(pastaDestino, "*.mp4", SearchOption.TopDirectoryOnly).ToList()
    '                         arquivosAudio = Directory.GetFiles(pastaDestino, "*.m4a", SearchOption.TopDirectoryOnly).ToList()


    '                         For Each video In arquivosVideo
    '                             Dim nomeBase = Path.GetFileNameWithoutExtension(video).Replace(".mp4", "").Replace(".webm", "")
    '                             Dim audio = arquivosAudio.FirstOrDefault(Function(a) Path.GetFileNameWithoutExtension(a).Contains(nomeBase))

    '                             If Not String.IsNullOrEmpty(audio) Then
    '                                 Dim outputFinal = Path.Combine(pastaDestino, nomeBase & "_merged.mp4")
    '                                 Dim ffmpegPath = Path.Combine(Application.StartupPath, "app", "ffmpeg.exe")
    '                                 Dim psi As New ProcessStartInfo(ffmpegPath, $"-y -i ""{video}"" -i ""{audio}"" -c copy ""{outputFinal}""") With {
    '                                 .CreateNoWindow = True,
    '                                 .UseShellExecute = False
    '                             }

    '                                 Using ffmpegProc As Process = Process.Start(psi)
    '                                     ffmpegProc.WaitForExit()
    '                                 End Using

    '                                 Try
    '                                     File.Delete(video)
    '                                     File.Delete(audio)
    '                                 Catch ex As Exception
    '                                     Me.Invoke(Sub() txtLog.AppendText($"[ERRO ao excluir .part] {ex.Message}" & Environment.NewLine))
    '                                 End Try

    '                                 Me.Invoke(Sub() txtLog.AppendText($"🎬 Merge finalizado: {Path.GetFileName(outputFinal)}" & Environment.NewLine))
    '                             End If
    '                         Next

    '                         Me.Invoke(Sub()
    '                                       progressBarDownload.Value = 0
    '                                       txtLog.AppendText(Environment.NewLine & "⛔ Download interrompido pelo usuário." & Environment.NewLine)
    '                                       btCancelar.Enabled = False
    '                                       btnExecutar.Enabled = True
    '                                       StatusLabel.Text = "Status: Cancelado pelo usuário."
    '                                       timerFakeProgress.Stop()
    '                                   End Sub)

    '                         If Directory.Exists(pastaDestino) AndAlso Directory.EnumerateFiles(pastaDestino).Any() Then
    '                             Process.Start("explorer.exe", pastaDestino)
    '                         Else
    '                             Me.Invoke(Sub() txtLog.AppendText("⚠️ Nenhum arquivo parcial encontrado." & Environment.NewLine))
    '                         End If

    '                     Catch ex As Exception
    '                         Me.Invoke(Sub()
    '                                       txtLog.AppendText($"[ERRO ao parar o processo] {ex.Message}" & Environment.NewLine)
    '                                       btCancelar.Enabled = False
    '                                       btnExecutar.Enabled = True
    '                                       timerFakeProgress.Stop()
    '                                   End Sub)
    '                     End Try
    '                 Else
    '                     Me.Invoke(Sub()
    '                                   txtLog.AppendText("⚠️ Nenhum processo ativo para interromper." & Environment.NewLine)
    '                                   btCancelar.Enabled = False
    '                                   btnExecutar.Enabled = True
    '                                   timerFakeProgress.Stop()
    '                               End Sub)
    '                 End If
    '             End Sub)
    'End Sub

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

        NotifyIcon1.Text = $"Download: {porcentagem}% completo"
    End Sub

    'Private Sub MonitorarClipboardTelegram()
    '    Dim texto As String = Clipboard.GetText()

    '    If texto.StartsWith("https://t.me/") OrElse texto.Contains("t.me/") Then
    '        If MessageBox.Show($"📋 Link do Telegram detectado:{Environment.NewLine}{texto}{Environment.NewLine}{Environment.NewLine}Deseja adicionar à lista de downloads?", "Novo Link Detectado", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
    '            addLink(texto)
    '        End If
    '    End If
    'End Sub

    Public Function ListViewContains(ByVal listView As ListView, ByVal linkProcurado As String) As Boolean
        For Each item As ListViewItem In listView.Items
            '  MsgBox($"Verificando item: {item.Tag.ToString} contra {linkProcurado}")

            If item.Tag.ToString.Equals(linkProcurado, StringComparison.OrdinalIgnoreCase) Then
                Return True ' Link encontrado
            End If
        Next
        Return False ' Link não encontrado
    End Function

    Private Async Sub TimerClipboard_Tick(sender As Object, e As EventArgs) Handles TimerClipboard.Tick
        Try
            If Clipboard.ContainsText() Then
                Dim linkDetected As String = Clipboard.GetText().Trim()
                'Debug.WriteLine($"Link detectado: {linkDetected}")
                If linkDetected.StartsWith("http", StringComparison.OrdinalIgnoreCase) AndAlso linkDetected <> ultimoLinkDetectado AndAlso ListViewContains(lstLink, linkDetected) = False Then

                    ultimoLinkDetectado = linkDetected

                    Dim resposta = MessageBox.Show($"Link detectado na área de transferência:{Environment.NewLine}{linkDetected}{Environment.NewLine}{Environment.NewLine}Deseja adicionar à lista de downloads?", "Novo Link Detectado", MessageBoxButtons.YesNo, MessageBoxIcon.Question)

                    If resposta = DialogResult.Yes Then
                        Await addLink(linkDetected)
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
