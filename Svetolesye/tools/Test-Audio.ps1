param([string]$AudioDirectory)
Add-Type -AssemblyName PresentationCore,WindowsBase
$results=@()
foreach($file in Get-ChildItem -LiteralPath $AudioDirectory -Filter *.wav) {
    $player=New-Object System.Windows.Media.MediaPlayer
    $script:opened=$false
    $script:failure=''
    $player.add_MediaOpened({$script:opened=$true})
    $player.add_MediaFailed({param($sender,$eventArgs) $script:failure=$eventArgs.ErrorException.Message})
    $player.Volume=0
    $player.Open([Uri]$file.FullName)
    $player.Play()
    $watch=[Diagnostics.Stopwatch]::StartNew()
    while(-not $script:opened -and -not $script:failure -and $watch.Elapsed.TotalSeconds -lt 5) {
        $frame=New-Object System.Windows.Threading.DispatcherFrame
        $timer=New-Object System.Windows.Threading.DispatcherTimer
        $timer.Interval=[TimeSpan]::FromMilliseconds(50)
        $timer.add_Tick({$frame.Continue=$false;$timer.Stop()})
        $timer.Start()
        [System.Windows.Threading.Dispatcher]::PushFrame($frame)
    }
    if(-not $script:opened){throw "Media decode failed: $($file.Name) $script:failure"}
    $results+="$($file.Name): decoded, duration $($player.NaturalDuration)"
    $player.Close()
}
$results
