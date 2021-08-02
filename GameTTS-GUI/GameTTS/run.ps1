$prevPwd = $PWD; Set-Location -ErrorAction Stop -LiteralPath $PSScriptRoot

try {
    # activate virtualenv
    .\.venv\Scripts\activate

    # run program
    python .\stdin_listener.py

    # deactivate virtualenv
    deactivate

}
finally {
    $prevPwd | Set-Location
}