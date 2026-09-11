health-scale-display =
    { $deltasign ->
        [-1] урон { $kind } на [color=green]x{ $amount }[/color]
         [0] урон { $kind } на x{ $amount }
         [1] урон { $kind } на [color=red]x{ $amount }[/color]
        *[other] урон { $kind } на x{ $amount }
    }

reagent-effect-guidebook-health-scale =
    { $chance ->
        [1] Умножает существующий { $changes }
       *[other] Имеет { $chance }% шанс умножить существующий { $changes }
    }
