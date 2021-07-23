$prevPwd = $PWD; Set-Location -ErrorAction Stop -LiteralPath $PSScriptRoot

try {
    # activate virtualenv
    .\.venv\Scripts\activate

    # run program
    python .\main.py $args[0] $args[1] $args[2] false $args[3] $args[4] $args[5] false "wav"

    # deactivate virtualenv
    deactivate
}
finally {
    $prevPwd | Set-Location
}