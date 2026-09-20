enum Direction { Down, Up, Left, Right }
enum GameState { Welcome, MainMenu, Playing, Paused, GameOver }
enum PlayerState { Idle, Walking, Running, Attacking, Dead }
/// <summary>Light: quick and cheap. Heavy: slow windup, double damage (and later the only one that staggers).</summary>
enum AttackKind { Light, Heavy }
enum DemonState { Idle, Dying, Attacking }
/// <summary>How a demon is drawn: the sprite sheet, or one of the primitive bodies in <see cref="EnemyIcons"/>.</summary>
enum EnemyLook { Sprite, Imp, Brute, Tank, Sniper, Warlord, Wisp }
