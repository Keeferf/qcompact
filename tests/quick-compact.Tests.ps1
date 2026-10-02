#Requires -Version 5.1
#Requires -Modules @{ ModuleName = 'Pester'; ModuleVersion = '5.0.0' }

BeforeAll {
    $script:ScriptPath = Join-Path $PSScriptRoot '..' 'quick-compact.ps1'
    . $script:ScriptPath
}

Describe 'Format-Size' {
    It 'formats plain bytes' {
        Format-Size 512 | Should -Be '512.00 B'
    }

    It 'formats kibibytes' {
        Format-Size 1024 | Should -Be '1.00 KB'
    }

    It 'formats gibibytes' {
        Format-Size 1073741824 | Should -Be '1.00 GB'
    }
}

Describe 'New-DiskpartScript' {
    It 'quotes the vhdx path and compacts it read-only' {
        $result = New-DiskpartScript -VhdxPath 'C:\Users\me\ext4.vhdx'

        $result | Should -Match 'select vdisk file="C:\\Users\\me\\ext4\.vhdx"'
        $result | Should -Match 'attach vdisk readonly'
        $result | Should -Match 'compact vdisk'
        $result | Should -Match 'detach vdisk'
    }
}

Describe 'Get-WslVhdxPaths' {
    BeforeEach {
        $script:TestRoot = "HKCU:\Software\quick-compact-test-$([guid]::NewGuid().ToString('N'))"
        $basePath = Join-Path $TestDrive 'dist'
        New-Item -ItemType Directory -Path $basePath -Force | Out-Null
        New-Item -ItemType File -Path (Join-Path $basePath 'ext4.vhdx') -Force | Out-Null

        $key = Join-Path $script:TestRoot '11111111-1111-1111-1111-111111111111'
        New-Item -Path $key -Force | Out-Null
        Set-ItemProperty -Path $key -Name DistributionName -Value 'TestDistro'
        Set-ItemProperty -Path $key -Name BasePath -Value $basePath
    }

    AfterEach {
        Remove-Item -Path $script:TestRoot -Recurse -Force -ErrorAction SilentlyContinue
    }

    It 'maps a registry BasePath to its ext4.vhdx' {
        $result = @(Get-WslVhdxPaths -RegistryRoot $script:TestRoot)

        $result.Count | Should -Be 1
        $result[0].Distro | Should -Be 'TestDistro'
        $result[0].Exists | Should -BeTrue
        $result[0].Vhdx | Should -Be (Join-Path $result[0].BasePath 'ext4.vhdx')
    }
}

Describe 'quick-compact.ps1' {
    It 'runs a dry run without throwing' {
        & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $script:ScriptPath -DryRun -NoElevate 2>&1 | Out-Null
        $LASTEXITCODE | Should -Be 0
    }
}
