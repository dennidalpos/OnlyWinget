@{
    ExcludeRules = @(
        # Build scripts use Write-Host intentionally for colored interactive output
        'PSAvoidUsingWriteHost'
        # Private helper functions inside scripts are not public cmdlets
        'PSUseShouldProcessForStateChangingFunctions'
        # False positive: parameters are used inside Invoke-Step scriptblocks that PSSA cannot trace
        'PSReviewUnusedParameter'
    )
}
