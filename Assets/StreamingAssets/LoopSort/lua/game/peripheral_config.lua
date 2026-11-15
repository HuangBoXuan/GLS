-- APK 4.6.0: Areas, Boosters, Constants, DailyChallengeRewards and DailyRewards tables.
local P={max_lives=5,life_seconds=1800,refill_cost=900,win_rewards={Default=10,Hard=30,SuperHard=50},
    boosters={undo={unlock=1,units=1,initial=2},shuffle={unlock=1,units=1,initial=2},belt={unlock=1,units=1,initial=2},truck={unlock=1,units=1,initial=2}},
    areas={
        {id=1,name='纽约',unlock=1},
        {id=2,name='巴黎',unlock=51},
        {id=3,name='伦敦',unlock=76},
        {id=4,name='罗马',unlock=131},
        {id=5,name='阿姆斯特丹',unlock=186},
        {id=6,name='东京',unlock=241},
        {id=7,name='伊斯坦布尔',unlock=296},
        {id=8,name='迪拜',unlock=351},
        {id=9,name='巴塞罗那',unlock=406},
        {id=10,name='旧金山',unlock=461},
        {id=11,name='香港',unlock=516},
        {id=12,name='布拉格',unlock=571},
        {id=13,name='首尔',unlock=626},
        {id=14,name='新加坡',unlock=681},
        {id=15,name='米兰',unlock=736},
        {id=16,name='马德里',unlock=791},
        {id=17,name='雅典',unlock=846},
        {id=18,name='悉尼',unlock=901},
        {id=19,name='曼谷',unlock=1001},
    },
    daily_milestones={{days=3,reward='gold:100'},{days=7,reward='life:30'},{days=14,reward='gold:500'},{days=25,reward='gold:2500'},},
    daily_rewards={{weight=100,reward='life:30'},{weight=120,reward='life:10'},{weight=120,reward='life:20'},{weight=120,reward='gold:50'},{weight=120,reward='gold:100'},{weight=120,reward='gold:200'},{weight=100,reward='undobooster:1'},{weight=100,reward='shufflebooster:1'},{weight=100,reward='capacitybooster:1'},{weight=100,reward='conveyorcapacitybooster:1'},},
}
return P

