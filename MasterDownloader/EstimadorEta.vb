Imports System.Globalization
Imports System.Text.RegularExpressions

Public Class EstimadorEta
    Private estimativa As Double?
    Private instanteAnterior As Double
    Private ultimoEta As String = "--:--"

    Public Sub Reiniciar()
        estimativa = Nothing
        ultimoEta = "--:--"
    End Sub

    Public Function Atualizar(linha As String, percentual As Double, instante As Double) As String
        If percentual >= 100 Then
            Reiniciar()
            Return "00:00"
        End If

        Dim match = Regex.Match(linha, "\bETA\s+(?:(\d+):)?(\d{1,2}):(\d{2})(?!\d)")
        If Not match.Success Then
            Return ultimoEta
        End If

        Dim segundos = Double.Parse(match.Groups(2).Value, CultureInfo.InvariantCulture) * 60 +
                       Double.Parse(match.Groups(3).Value, CultureInfo.InvariantCulture)
        If match.Groups(1).Success Then
            segundos += Double.Parse(match.Groups(1).Value, CultureInfo.InvariantCulture) * 3600
        End If

        If estimativa.HasValue Then
            Dim intervalo = Math.Max(0, instante - instanteAnterior)
            ' Média exponencial com janela de 10 segundos, independente da frequência do log.
            Dim peso = 1 - Math.Exp(-intervalo / 10)
            estimativa = Math.Max(0, estimativa.Value - intervalo) * (1 - peso) + segundos * peso
        Else
            estimativa = segundos
        End If
        instanteAnterior = instante

        Dim total = CLng(Math.Ceiling(estimativa.Value))
        If total >= 3600 Then
            ultimoEta = $"{total \ 3600:D2}:{(total Mod 3600) \ 60:D2}:{total Mod 60:D2}"
        Else
            ultimoEta = $"{total \ 60:D2}:{total Mod 60:D2}"
        End If
        Return ultimoEta
    End Function
End Class
