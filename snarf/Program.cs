using System;
using System.IO;
using System.Media;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Collections.Generic;
using DSound = Microsoft.DirectX.DirectSound;

class Form1 : Form
{
    public const string TITLE = "Snarf";

    //Timers
    const int INT_MOVE = 40; //25 FPS
    const int INT_DRAW = 100; //10 FPS
    const int INT_FNSH = 50;
    const int INT_SNRF = 2000;
    const int INT_SPWN = 200;
    const int INT_EXPL = 150;
    const int INT_HEAL = 50;

    //Player, score
    const int MAX_TAGS = 50;
    const int SCORE_CROWN = 100;
    const int SCORE_RING1 = 10;
    const int SCORE_RING2 = 5;
    const int SCORE_RING3 = 1;
    const int MAX_SNARFS = 16;
    const int MAX_SHOTS = 2;
    const int MAX_SNTIME = 4;
    const int MAX_SNHEALTH = 4;

    //Game area
    const int WALLS_HOR = 40;
    const int WALLS_VER = 18;
    const int IMG_SIZE = 16;
    const int START_X = 8;
    const int START_Y = 38;
    const int LEN_X = WALLS_HOR * IMG_SIZE - IMG_SIZE;
    const int LEN_Y = WALLS_VER * IMG_SIZE;
    const int END_X = START_X + LEN_X;
    const int END_Y = START_Y + LEN_Y;

    //Numer width
    const int NUM_WIDTH = 12;

    //Game data folders
    const string FLD_GFX = "gfx\\";
    const string FLD_SND = "snd\\";

    //EGA palette from the game
    static Color[] EGA_PALETTE =
    {
        Color.FromArgb(0,0,0),
        Color.FromArgb(170,85,0),
        Color.FromArgb(255,0,0),
        Color.FromArgb(255,85,0),
        Color.FromArgb(255,170,0),
        Color.FromArgb(255,255,0),
        Color.FromArgb(0,255,0),
        Color.FromArgb(0,170,85),
        Color.FromArgb(0,255,255),
        Color.FromArgb(0,0,255),
        Color.FromArgb(170,0,255),
        Color.FromArgb(255,0,170),
        Color.FromArgb(255,170,170),
        Color.FromArgb(170,170,170),
        Color.FromArgb(255,255,255)
    };

    Graphics gfx;
    SolidBrush brs_clr = new SolidBrush(EGA_PALETTE[0]);
    SolidBrush brs_fnt = new SolidBrush(EGA_PALETTE[3]);

    Bitmap wall;
    Bitmap expl;
    Bitmap img_splash;
    Bitmap img_game;
    Bitmap img_end;

    Bitmap[] hero;
    Bitmap[] snarf;
    Bitmap[] shot;

    Font fnt_game;
    Bitmap[] numbers;
    Rectangle[] fields =
    {
        //Upper
        new Rectangle(22,13,30,16),
        new Rectangle(84,13,54,16),
        new Rectangle(170,13,78,16),
        new Rectangle(280,13,114,16),
        new Rectangle(426,13,42,16),
        new Rectangle(500,13,114,16),
        //Lower
        new Rectangle(55,334,147,12),
        new Rectangle(444,334,147,12)
    };

    Sprite player;
    List<FSprite> shots;
    List<PSprite> game_snarfs;

    List<FSprite> game_spr = new List<FSprite>();
    List<Warp> game_tele = new List<Warp>();
    List<Sprite> game_wall = new List<Sprite>();
    List<PSprite> game_pits = new List<PSprite>();
    List<FSprite> game_expl;

    Dictionary<int, Bitmap> game_objs = new Dictionary<int, Bitmap>() 
    {
        {0x30,new Bitmap(FLD_GFX+"hero1.png")},
        {0x41,new Bitmap(FLD_GFX+"ring1.png")},
        {0x42,new Bitmap(FLD_GFX+"ring2.png")},
        {0x43,new Bitmap(FLD_GFX+"ring3.png")},
        {0x40,new Bitmap(FLD_GFX+"crown.png")},
        {0x60,new Bitmap(FLD_GFX+"key.png")},
        {0x70,new Bitmap(FLD_GFX+"lock.png")},
        {0x71,new Bitmap(FLD_GFX+"lock.png")},
        {0x80,new Bitmap(FLD_GFX+"firstaid.png")}
    };

    //Moving
    Keys[] movekeys =
    {
        Keys.Up,
        Keys.Down,
        Keys.Left,
        Keys.Right
    };

    Keys[] firekeys =
    {
        Keys.W,
        Keys.S,
        Keys.A,
        Keys.D
    };

    //Player moving
    int[,] move =
    {
        {0,-2,0},
        {0,2,2},
        {-2,0,2},
        {2,0,0}
    };

    //Snarf spawning
    int[,] posoffs =
    {
        {0,1},
        {0,-1},
        {-1,0},
        {1,0}
    };

    int dir = -1;
    int rqdir = -1;
    byte step = 0;
    bool haskey = false;
    int anim_offs = 0;

    //Game handling
    Timer tmr_move;
    Timer tmr_draw;
    Timer tmr_fnsh;
    Timer tmr_snrf;
    Timer tmr_spwn;
    Timer tmr_expl;
    Timer tmr_heal;

    //Player info
    int tags = MAX_TAGS;
    int tmptags;
    int points = 0;
    int score = 0;
    int levscore = 0;
    int highscore = 0;
    int level;

    //Random
    Random rnd;

    //Snarf spawn
    int sntime = MAX_SNTIME;
    int snpit = -1;

    //Splash, endgame menu
    byte state;

    //Paused
    bool pause;

    //Sound handling
    DSound.Device snddev;

    //Managed sounds
    DSound.Buffer snd_phit;

    public Form1(int lvl)
    {
        //Init DirectSound
        snddev = new DSound.Device();
        snddev.SetCooperativeLevel(Handle, DSound.CooperativeLevel.Priority);
        snd_phit = new DSound.Buffer(FLD_SND + "hit.wav", snddev);
        level = lvl;
        Text = TITLE;
        Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        BackColor = EGA_PALETTE[(tags + 7) % 13 + 1];
        img_splash = new Bitmap(FLD_GFX + "main.png");
        img_game = new Bitmap(FLD_GFX + "game.png");
        img_end = new Bitmap(FLD_GFX + "end.png");
        BackgroundImage = new Bitmap(img_splash);
        BackgroundImageLayout = ImageLayout.Zoom;
        gfx = Graphics.FromImage(BackgroundImage);
        gfx.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        gfx.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
        gfx.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.None;
        gfx.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
        ClientSize = BackgroundImage.Size;
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint, true);
        rnd = new Random();
        state = 0;
        CenterToScreen();
    }

    void LoadGame(int lvl)
    {
        BackColor = EGA_PALETTE[(tags + 7) % 13 + 1];
        BackgroundImage = new Bitmap(img_game);
        gfx = Graphics.FromImage(BackgroundImage);
        Bitmap tmp, tmp2;
        Graphics tmpb;
        //
        tmp = new Bitmap(FLD_GFX + "numbs.png");
        numbers = new Bitmap[10];
        for (int i = 0; i < numbers.Length; i++)
        {
            tmp2 = new Bitmap(NUM_WIDTH, IMG_SIZE);
            tmpb = Graphics.FromImage(tmp2);
            tmpb.DrawImage(tmp,
                new Rectangle(0, 0, NUM_WIDTH, IMG_SIZE),
                new Rectangle(i * NUM_WIDTH, 0, NUM_WIDTH, IMG_SIZE),
                GraphicsUnit.Pixel);
            numbers[i] = tmp2;
        }
        //Hero normal
        hero = new Bitmap[8]; //0-3 w/o key, 4-7 w key
        tmp = new Bitmap(FLD_GFX + "hero1.png");
        hero[0] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        hero[2] = tmp;
        tmp = new Bitmap(FLD_GFX + "hero2.png");
        hero[1] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        hero[3] = tmp;
        //Hero with key
        tmp = new Bitmap(FLD_GFX + "herok1.png");
        hero[4] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        hero[6] = tmp;
        tmp = new Bitmap(FLD_GFX + "herok2.png");
        hero[5] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.RotateNoneFlipX);
        hero[7] = tmp;
        //Snarf
        tmp = new Bitmap(FLD_GFX + "snarf1.png");
        snarf = new Bitmap[4];
        snarf[1] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        snarf[0] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
        snarf[2] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        snarf[3] = tmp;
        //Exploded snarf
        expl = new Bitmap(FLD_GFX + "snarf2.png");
        //Player shot
        shot = new Bitmap[4];
        tmp = new Bitmap(FLD_GFX + "shot.png");
        shot[1] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        shot[0] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
        shot[2] = tmp;
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        shot[3] = tmp;
        //Teleport
        tmp = new Bitmap(FLD_GFX + "teleport.png");
        game_objs.Add(0x11, tmp);
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        game_objs.Add(0x10, tmp);
        //Snarf pit
        tmp = new Bitmap(FLD_GFX + "snarfp1.png");
        game_objs.Add(0x23, tmp);
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        game_objs.Add(0x21, tmp);
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
        game_objs.Add(0x22, tmp);
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        game_objs.Add(0x20, tmp);
        //
        tmp = new Bitmap(FLD_GFX + "snarfp2.png");
        game_objs.Add(0x27, tmp);
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        game_objs.Add(0x25, tmp);
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate270FlipNone);
        game_objs.Add(0x26, tmp);
        tmp = new Bitmap(tmp);
        tmp.RotateFlip(RotateFlipType.Rotate180FlipNone);
        game_objs.Add(0x24, tmp);
        //
        tmp = null;
        tmpb = null;
        //Other
        fnt_game = new Font(FontFamily.GenericMonospace, 8);
        shots = new List<FSprite>(MAX_SHOTS);
        game_snarfs = new List<PSprite>(MAX_SNARFS);
        game_expl = new List<FSprite>();
        //Player data
        DrawStats();
        //Load level, if not found load endgame
        if (!LoadLevel(lvl)) return;
        //Timers
        //Dynamic object moving
        tmr_move = new Timer();
        tmr_move.Interval = INT_MOVE; //25 FPS
        tmr_move.Tick += tmr_move_Tick;
        tmr_move.Start();
        //Object drawing and player damage
        tmr_draw = new Timer();
        tmr_draw.Interval = INT_DRAW; //10 FPS
        tmr_draw.Tick += tmr_draw_Tick;
        tmr_draw.Start();
        //Finish animation
        tmr_fnsh = new Timer();
        tmr_fnsh.Interval = INT_FNSH;
        tmr_fnsh.Tick += tmr_fnsh_Tick;
        //Snarf pit trigger
        tmr_snrf = new Timer();
        tmr_snrf.Interval = INT_SNRF;
        tmr_snrf.Tick += tmr_snrf_Tick;
        tmr_snrf.Start();
        //Snarf spawning
        tmr_spwn = new Timer();
        tmr_spwn.Interval = INT_SPWN;
        tmr_spwn.Tick += tmr_spwn_Tick;
        //Snarf explosion
        tmr_expl = new Timer();
        tmr_expl.Interval = INT_EXPL;
        tmr_expl.Tick += tmr_expl_Tick;
        tmr_expl.Start();
        //Health pickup animation
        tmr_heal = new Timer();
        tmr_heal.Interval = INT_HEAL;
        tmr_heal.Tick += tmr_heal_Tick;
    }

    //Timer handling
    void tmr_expl_Tick(object sender, EventArgs e)
    {
        //2 pass to ensure it draws out
        for (int i = game_expl.Count - 1; i >= 0; i--)
        {
            if (game_expl[i].fnc == 0xFF)
                game_expl[i].fnc = 0;
            else
                game_expl.Remove(game_expl[i]);
        }
    }

    void tmr_snrf_Tick(object sender, EventArgs e)
    {
        if (game_snarfs.Count < MAX_SNARFS)
        {
            snpit = rnd.Next(0, game_pits.Count);
            tmr_spwn.Start();
            tmr_snrf.Stop();
        }
        for (int i = game_snarfs.Count - 1; i >= 0; i--)
        {
            if (game_snarfs[i].fnc == 1)
                DestroySnarf(i);
            else
                game_snarfs[i].fnc--;
        }
    }

    void tmr_spwn_Tick(object sender, EventArgs e)
    {
        if (snpit == -1 || game_pits.Count == 0) return;

        sntime--;
        game_pits[snpit].img = game_objs[game_pits[snpit].fnc + 4 * (sntime % 2)];
        if (sntime == 0)
        {
            sntime = game_pits[snpit].dir;
            game_snarfs.Add(new PSprite()
                {
                    pos = new Rectangle(
                        game_pits[snpit].pos.X + posoffs[sntime, 0] * IMG_SIZE,
                        game_pits[snpit].pos.Y + posoffs[sntime, 1] * IMG_SIZE,
                        IMG_SIZE, IMG_SIZE),
                    img = snarf[sntime],
                    fnc = MAX_SNHEALTH,
                    dir = (byte)sntime
                });
            snpit = -1;
            sntime = MAX_SNTIME;
            tmr_snrf.Start();
            tmr_spwn.Stop();
        }
    }

    void tmr_heal_Tick(object sender, EventArgs e)
    {
        tags++;
        DrawNumbs(0, tags.ToString());
        BackColor = EGA_PALETTE[(tags + 7) % 13 + 1];
        if (tags == MAX_TAGS)
        {
            tmr_heal.Stop();
            tmr_snrf.Start();
            tmr_spwn.Start();
            tmr_move.Start();
            tmr_draw.Start();
        }
    }

    void tmr_fnsh_Tick(object sender, EventArgs e)
    {
        tmptags--;
        DrawNumbs(0, tmptags.ToString());
        BackColor = EGA_PALETTE[(tmptags + 7) % 13 + 1];
        if (tmptags == 0)
        {
            game_wall.Clear();
            gfx.DrawImage(img_game, 0, 0, img_game.Width, img_game.Height);
            BackColor = EGA_PALETTE[(tags + 7) % 13 + 1];
            LoadLevel(++level);
            haskey = false;
            DrawStats();
            tmr_move.Start();
            tmr_draw.Start();
            tmr_snrf.Start();
            tmr_fnsh.Stop();
        }
    }

    void tmr_move_Tick(object sender, EventArgs e)
    {
        if (dir != -1)
        {
            player.pos.X += move[dir, 0];
            player.pos.Y += move[dir, 1];
        }
        if (rqdir != -1)
        {
            if ((player.pos.X - START_X) % 16 == 0 && (player.pos.Y - START_Y) % 16 == 0)
            {
                dir = rqdir;
                rqdir = -1;
            }
        }
        ShotCollisionCheck();
        PlayerCollisionCheck();
        //Snarf movement
        foreach (PSprite snarf in game_snarfs)
        {
            if (snarf.dir < 4)
            {
                snarf.pos.X += move[snarf.dir, 0];
                snarf.pos.Y += move[snarf.dir, 1];
            }
            SnarfCollisionCheck(snarf);
        }
    }

    private void ShotCollisionCheck()
    {
        int i;
        for (i = 0; i < shots.Count; i++)
        {
            shots[i].pos.X += move[shots[i].fnc, 0] * 2;
            shots[i].pos.Y += move[shots[i].fnc, 1] * 2;
            //Level bounds
            if (shots[i].pos.X < START_X)
            {
                shots.RemoveAt(i);
                continue;
            }
            else if (shots[i].pos.Right > END_X)
            {
                shots.RemoveAt(i);
                continue;
            }
            if (shots[i].pos.Y < START_Y)
            {
                shots.RemoveAt(i);
                continue;
            }
            else if (shots[i].pos.Bottom > END_Y)
            {
                shots.RemoveAt(i);
                continue;
            }
            ShotCollisionCheck2(i);
        }
    }

    private void ShotCollisionCheck2(int i)
    {
        int j;
        //Other
        for (j = 0; j < game_wall.Count; j++)
        {
            if (shots[i].pos.IntersectsWith(game_wall[j].pos))
            {
                shots.RemoveAt(i);
                return;
            }
        }
        for (j = 0; j < game_pits.Count; j++)
        {
            if (shots[i].pos.IntersectsWith(game_pits[j].pos))
            {
                shots.RemoveAt(i);
                return;
            }
        }
        for (j = 0; j < game_tele.Count; j++)
        {
            if (shots[i].pos.IntersectsWith(game_tele[j].pos))
            {
                shots.RemoveAt(i);
                return;
            }
        }
        for (j = 0; j < game_spr.Count; j++)
        {
            if (shots[i].pos.IntersectsWith(game_spr[j].pos))
            {
                shots.RemoveAt(i);
                return;
            }
        }
        for (j = 0; j < game_snarfs.Count; j++)
        {
            if (shots[i].pos.IntersectsWith(game_snarfs[j].pos))
            {
                DestroySnarf(j);
                shots.RemoveAt(i);
                return;
            }
        }
    }

    private void PlayerCollisionCheck()
    {
        int i;
        //Level bounds
        if (player.pos.X < START_X)
        {
            player.pos.X = START_X;
            StopPlayer();
        }
        else if (player.pos.Right > END_X)
        {
            player.pos.X = END_X - IMG_SIZE;
            StopPlayer();
            PlaySound2(true, tags);
            tmptags = tags;
            tmr_snrf.Stop();
            tmr_spwn.Stop();
            tmr_move.Stop();
            tmr_draw.Stop();
            //Clear level
            game_pits.Clear();
            game_expl.Clear();
            game_spr.Clear();
            game_tele.Clear();
            game_snarfs.Clear();
            shots.Clear();
            //Redraw
            tmr_draw_Tick(null, null);
            //Load finish seq
            tmr_fnsh.Start();
        }
        if (player.pos.Y < START_Y)
        {
            player.pos.Y = START_Y;
            StopPlayer();
        }
        else if (player.pos.Bottom > END_Y)
        {
            player.pos.Y = END_Y - IMG_SIZE;
            StopPlayer();
        }
        //Wall collisions
        for (i = 0; i < game_wall.Count; i++)
        {
            if (player.pos.IntersectsWith(game_wall[i].pos))
            {
                CheckCollision(game_wall[i].pos);
                return;
            }
        }
        //Snarf pits
        for (i = 0; i < game_pits.Count; i++)
        {
            if (player.pos.IntersectsWith(game_pits[i].pos))
            {
                CheckCollision(game_pits[i].pos);
                return;
            }
        }
        //Teleport
        for (i = 0; i < game_tele.Count; i++)
        {
            if (player.pos.IntersectsWith(game_tele[i].pos))
            {
                PlaySound("teleport.wav");
                player.pos.X = game_tele[i].pos2.X;
                player.pos.Y = game_tele[i].pos2.Y;
                return;
            }
        }
        //Other items
        for (i = 0; i < game_spr.Count; i++)
        {
            if (player.pos.IntersectsWith(game_spr[i].pos))
            {
                switch (game_spr[i].fnc)
                {
                    case 0x60:
                        if (haskey)
                            return;
                        else
                        {
                            haskey = true;
                            anim_offs = 4;
                        }
                        break;
                    case 0x70:
                    case 0x71:
                        if (haskey)
                        {
                            haskey = false;
                            anim_offs = 0;
                            PlaySound("door.wav");
                            game_spr.RemoveAt(i);
                        }
                        else
                            CheckCollision(game_spr[i].pos);
                        return;
                    case 0x80:
                        if (tags < MAX_TAGS)
                        {
                            PlaySound2(false, tags);
                            game_spr.RemoveAt(i);
                            tmr_heal.Start();
                            tmr_snrf.Stop();
                            tmr_spwn.Stop();
                            tmr_move.Stop();
                            tmr_draw.Stop();
                        }
                        return;
                    case 0x40:
                        points += SCORE_CROWN;
                        DrawNumbs(1, points.ToString());
                        break;
                    case 0x41:
                        points += SCORE_RING1;
                        DrawNumbs(1, points.ToString());
                        break;
                    case 0x42:
                        points += SCORE_RING2;
                        DrawNumbs(1, points.ToString());
                        break;
                    case 0x43:
                        points += SCORE_RING3;
                        DrawNumbs(1, points.ToString());
                        break;
                }
                PlaySound("pick.wav");
                game_spr.RemoveAt(i);
                return;
            }
        }
    }

    private void SnarfCollisionCheck(PSprite snarf)
    {
        int i;
        //Wall collisions
        for (i = 0; i < game_wall.Count; i++)
        {
            if (snarf.pos.IntersectsWith(game_wall[i].pos))
            {
                CheckCollision(snarf, game_wall[i].pos);
                return;
            }
        }
        //Snarf pits
        for (i = 0; i < game_pits.Count; i++)
        {
            if (snarf.pos.IntersectsWith(game_pits[i].pos))
            {
                CheckCollision(snarf, game_pits[i].pos);
                return;
            }
        }
        //Teleport
        for (i = 0; i < game_tele.Count; i++)
        {
            if (snarf.pos.IntersectsWith(game_tele[i].pos))
            {
                CheckCollision(snarf, game_tele[i].pos);
                return;
            }
        }
    }

    void tmr_draw_Tick(object sender, EventArgs e)
    {
        gfx.FillRectangle(brs_clr, START_X, START_Y, LEN_X, LEN_Y);
        DrawGame();
        //Player draw
        if (dir != -1)
        {
            player.img = hero[move[dir, 2] + step + anim_offs];
            step++;
            step %= 2;
        }
        gfx.DrawImage(player.img, player.pos);
        //Player draw end
        Invalidate();
        //Damage
        if (tags == 0) return;
        //Snarfs
        for (int i = 0; i < game_snarfs.Count; i++)
        {
            if (player.pos.IntersectsWith(game_snarfs[i].pos))
            {
                tags--;
                DrawNumbs(0, tags.ToString());
                snd_phit.Play(0, DSound.BufferPlayFlags.Default);
                if (tags > 0)
                    BackColor = EGA_PALETTE[(tags + 7) % 13 + 1];
                else
                    BackColor = EGA_PALETTE[0];
                return;
            }
        }
    }

    //Functions
    void CheckCollision(Rectangle trg)
    {
        if (dir == 0 && player.pos.Top < trg.Bottom)
        {
            player.pos.Y = trg.Bottom;
            StopPlayer();
        }
        else if (dir == 1 && player.pos.Bottom > trg.Top)
        {
            player.pos.Y = trg.Top - IMG_SIZE;
            StopPlayer();
        }
        else if (dir == 2 && player.pos.Left < trg.Right)
        {
            player.pos.X = trg.Right;
            StopPlayer();
        }
        else if (dir == 3 && player.pos.Right > trg.Left)
        {
            player.pos.X = trg.Left - IMG_SIZE;
            StopPlayer();
        }
    }

    void CheckCollision(PSprite src, Rectangle trg)
    {
        if (src.dir == 0 && src.pos.Top < trg.Bottom)
        {
            src.pos.Y = trg.Bottom;
            src.dir = (byte)(++src.dir % 4);
        }
        else if (src.dir == 1 && src.pos.Bottom > trg.Top)
        {
            src.pos.Y = trg.Top - IMG_SIZE;
            src.dir = (byte)(++src.dir % 4);
        }
        else if (src.dir == 2 && src.pos.Left < trg.Right)
        {
            src.pos.X = trg.Right;
            src.dir = (byte)(++src.dir % 4);
        }
        else if (src.dir == 3 && src.pos.Right > trg.Left)
        {
            src.pos.X = trg.Left - IMG_SIZE;
            src.dir = (byte)(++src.dir % 4);
        }
    }

    void StopPlayer()
    {
        if (dir == -1) return;
        player.img = hero[move[dir, 2] + anim_offs];
        step = 0;
        dir = -1;
        rqdir = -1;
    }

    void DestroySnarf(int idx)
    {
        PlaySound("exp.wav");
        game_expl.Add(new FSprite()
        {
            img = expl,
            pos = game_snarfs[idx].pos
        });
        game_snarfs.RemoveAt(idx);
    }

    //Key handling
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (state == 0)
        {
            state = 1;
            LoadGame(level);
        }
        else if (state == 0xFF)
            Application.Exit();
        else
        {
            if (keyData == Keys.Space && !pause)
            {
                tmr_snrf.Stop();
                tmr_spwn.Stop();
                tmr_move.Stop();
                tmr_draw.Stop();
                pause = true;
            }
            else if (pause)
            {
                tmr_snrf.Start();
                tmr_spwn.Start();
                tmr_move.Start();
                tmr_draw.Start();
                pause = false;
            }
            else if (!CheckMoveFireKeys(keyData))
            {
                StopPlayer();
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private bool CheckMoveFireKeys(Keys keyData)
    {
        for (int i = 0; i < movekeys.Length; i++)
        {
            if (keyData == movekeys[i])
            {
                if (dir == -1)
                {
                    dir = i;
                    return true;
                }
                else if (dir != i)
                {
                    rqdir = i;
                    return true;
                }
            }
        }
        for (byte i = 0; i < movekeys.Length; i++)
        {
            if (keyData == firekeys[i])
            {
                if (shots.Count < MAX_SHOTS)
                {
                    shots.Add(new FSprite()
                    {
                        fnc = i,
                        pos = new Rectangle(
                            (player.pos.X - START_X) / 16 * 16 + START_X,
                            (player.pos.Y - START_Y) / 16 * 16 + START_Y,
                            IMG_SIZE, IMG_SIZE),
                        img = shot[i]
                    });
                }
                else
                {
                    PlaySound("empty.wav");
                }
                return true;
            }
        }
        return false;
    }

    //Level loading
    bool LoadLevel(int lvl)
    {
        pause = false;
        string fname;
        fname = "SNARFLEV." + lvl.ToString("000");
        if (!File.Exists(fname))
        {
            if (tmr_draw != null)
            {
                tmr_draw.Stop();
                tmr_expl.Stop();
                tmr_fnsh.Stop();
                tmr_heal.Stop();
                tmr_snrf.Stop();
                tmr_spwn.Stop();
            }
            state = 0xFF;
            BackgroundImage = new Bitmap(img_end);
            return false;
        }
        //Load wall bitmap
        BinaryReader br = new BinaryReader(new FileStream(fname, FileMode.Open, FileAccess.Read));
        DrawLevelData(ConvertToASCII(br.ReadBytes(0x18)),
            ConvertToASCII(br.ReadBytes(0x18)));
        br.BaseStream.Position = 0x38;
        int i, j, k, idx;
        short tmp;
        int[,] img_arr = new int[IMG_SIZE, IMG_SIZE];
        idx = IMG_SIZE - 1;
        for (i = 0; i < IMG_SIZE; i++)
        {
            for (k = 0; k < 4; k++)
            {
                tmp = (short)(br.ReadByte() << 8 | br.ReadByte());
                for (j = 0; j < IMG_SIZE; j++)
                {
                    if ((tmp & 1 << j) > 0)
                        img_arr[idx - j, i] |= (1 << (1 * k));
                }
            }
        }
        //Create wall bitmap
        wall = new Bitmap(IMG_SIZE, IMG_SIZE);
        for (i = 0; i < IMG_SIZE; i++)
            for (j = 0; j < IMG_SIZE; j++)
                wall.SetPixel(j, i, EGA_PALETTE[img_arr[j, i]]);
        //Load walls
        ulong wtmp;
        idx = WALLS_HOR - 1;
        for (i = 0; i < WALLS_VER; i++)
        {
            wtmp = (ulong)br.ReadByte() << 32;
            wtmp |= (ulong)br.ReadByte() << 24;
            wtmp |= (ulong)br.ReadByte() << 16;
            wtmp |= (ulong)br.ReadByte() << 8;
            wtmp |= (ulong)br.ReadByte() & 254;
            for (j = 0; j < WALLS_HOR; j++)
                if ((wtmp & 1ul << j) > 0)
                    game_wall.Add(new Sprite()
                        {
                            pos = new Rectangle(START_X + (idx - j) * IMG_SIZE, START_Y + i * IMG_SIZE, IMG_SIZE, IMG_SIZE),
                            img = wall
                        });
        }
        //Load sprites
        byte[] data;
        //Hero
        if (br.PeekChar() > -1)
        {
            data = br.ReadBytes(4);
            player = new Sprite()
            {
                pos = new Rectangle(START_X + (data[3] - 1) * IMG_SIZE, START_Y + (data[2] - 1) * IMG_SIZE, IMG_SIZE, IMG_SIZE),
                img = hero[0]
            };
        }
        //Other
        byte tmpdir;
        Warp twrp;
        while (br.PeekChar() > -1)
        {
            data = br.ReadBytes(4);
            if (data.Length < 4) break;
            if (!game_objs.ContainsKey(data[0])) continue;
            //Snarf pits
            if (data[0] >= 0x20 && data[0] <= 0x23)
            {
                switch (data[0])
                {
                    case 0x20:
                        tmpdir = 3;
                        break;
                    case 0x21:
                        tmpdir = 1;
                        break;
                    case 0x22:
                        tmpdir = 2;
                        break;
                    default:
                        tmpdir = 0;
                        break;
                }
                game_pits.Add(new PSprite()
                {
                    pos = new Rectangle(START_X + (data[3] - 1) * IMG_SIZE,
                        START_Y + (data[2] - 1) * IMG_SIZE, IMG_SIZE, IMG_SIZE),
                    img = game_objs[data[0]],
                    fnc = data[0],
                    dir = tmpdir
                });
            }
            else if (data[0] == 0x10)
            {
                twrp = new Warp()
                {
                    pos = new Rectangle(START_X + (data[3] - 1) * IMG_SIZE,
                        START_Y + (data[2] - 1) * IMG_SIZE, IMG_SIZE, IMG_SIZE),
                    img = game_objs[data[0]]
                };
                game_tele.Add(twrp);
                data = br.ReadBytes(4);
                twrp.pos2 = new Point(START_X + ((data[0] >> 4) + 0x10 * data[1]) * IMG_SIZE,
                        START_Y + ((data[2] >> 4) + 1) * IMG_SIZE);
            }
            else if (data[0] == 0x11)
            {
                twrp = new Warp()
                {
                    pos = new Rectangle(START_X + (data[3] - 1) * IMG_SIZE,
                        START_Y + (data[2] - 1) * IMG_SIZE, IMG_SIZE, IMG_SIZE),
                    img = game_objs[data[0]]
                };
                game_tele.Add(twrp);
                data = br.ReadBytes(4);
                twrp.pos2 = new Point(START_X + ((data[0] >> 4) + 0x10 * data[1]) * IMG_SIZE,
                        START_Y + ((data[2] >> 4)) * IMG_SIZE);
            }
            else
            {
                game_spr.Add(new FSprite()
                {
                    fnc = data[0],
                    pos = new Rectangle(START_X + (data[3] - 1) * IMG_SIZE,
                        START_Y + (data[2] - 1) * IMG_SIZE, IMG_SIZE, IMG_SIZE),
                    img = game_objs[data[0]]
                });
            }
        }
        twrp = null;
        br.Close();
        //Draw game elements
        DrawGame();
        return true;
    }

    //Draw functions
    void DrawGame()
    {
        int i;
        for (i = 0; i < game_spr.Count; i++)
            gfx.DrawImage(game_spr[i].img, game_spr[i].pos);
        for (i = 0; i < game_pits.Count; i++)
            gfx.DrawImage(game_pits[i].img, game_pits[i].pos);
        for (i = 0; i < game_tele.Count; i++)
            gfx.DrawImage(game_tele[i].img, game_tele[i].pos);
        for (i = 0; i < game_wall.Count; i++)
            gfx.DrawImage(game_wall[i].img, game_wall[i].pos);
        for (i = 0; i < shots.Count; i++)
            gfx.DrawImage(shots[i].img, shots[i].pos);
        for (i = 0; i < game_snarfs.Count; i++)
        {
            game_snarfs[i].img = snarf[game_snarfs[i].dir];
            gfx.DrawImage(game_snarfs[i].img, game_snarfs[i].pos);
        }
        for (i = 0; i < game_expl.Count; i++)
            gfx.DrawImage(expl, game_expl[i].pos);
        /*//Debugging teleport positions
        for (i = 0; i < game_tele.Count; i++)
        {
            gfx.DrawImage(game_tele[i].img, game_tele[i].pos);
            gfx.FillRectangle(Brushes.Red, game_tele[i].pos2.X, game_tele[i].pos2.Y, 16, 16);
        }*/
    }

    void DrawNumbs(int idx, string nm)
    {
        int tmp = fields[idx].Right - nm.Length * NUM_WIDTH;
        gfx.FillRectangle(brs_clr, fields[idx]);
        for (int i = 0; i < nm.Length; i++)
        {
            gfx.DrawImage(numbers[nm[i] - 0x30], tmp, fields[idx].Y, NUM_WIDTH, IMG_SIZE);
            tmp += 12;
        }
    }

    void DrawStats()
    {
        DrawNumbs(0, tags.ToString());
        DrawNumbs(1, points.ToString());
        DrawNumbs(2, levscore.ToString());
        DrawNumbs(3, score.ToString());
        DrawNumbs(4, level.ToString());
        DrawNumbs(5, highscore.ToString());
    }

    void DrawLevelData(string name, string auth)
    {
        gfx.DrawString(name, fnt_game, brs_fnt, fields[6]);
        gfx.DrawString(auth, fnt_game, brs_fnt, fields[7]);
    }

    void PlaySound(string snd)
    {
        DSound.Buffer buffer = new DSound.Buffer(FLD_SND + snd, snddev);
        buffer.Play(0, DSound.BufferPlayFlags.Default);
    }

    void PlaySound2(bool snd, int tags)
    {
        DSound.Buffer buffer;
        if (snd)
        {
            buffer = new DSound.Buffer(FLD_SND + "finish.wav", snddev);
            buffer.SetCurrentPosition((int)((1 - ((float)tags / MAX_TAGS)) * buffer.Caps.BufferBytes));
        }
        else
        {
            buffer = new DSound.Buffer(FLD_SND + "firstaid.wav", snddev);
            buffer.SetCurrentPosition((int)(((float)tags / MAX_TAGS) * buffer.Caps.BufferBytes));
        }
        buffer.Play(0, DSound.BufferPlayFlags.Default);
    }

    //Other
    string ConvertToASCII(byte[] data)
    {
        string tmp = null;
        for (int i = 0; i < data.Length; i++)
            tmp += (char)data[i];
        return tmp;
    }
}

//Helper classes
class Sprite
{
    public Rectangle pos;
    public Bitmap img;
}

class FSprite : Sprite
{
    public byte fnc = 0xFF;
}

class PSprite : FSprite
{
    public byte dir;
}

class Warp : Sprite
{
    public Point pos2;
}

class Program
{
    public static string[] flist =
    {
        "gfx\\crown.png",
        "gfx\\firstaid.png",
        "gfx\\game.png",
        "gfx\\hero1.png",
        "gfx\\hero2.png",
        "gfx\\herok1.png",
        "gfx\\herok2.png",
        "gfx\\key.png",
        "gfx\\lock.png",
        "gfx\\main.png",
        "gfx\\numbs.png",
        "gfx\\ring1.png",
        "gfx\\ring2.png",
        "gfx\\ring3.png",
        "gfx\\shot.png",
        "gfx\\snarf1.png",
        "gfx\\snarf2.png",
        "gfx\\snarfp1.png",
        "gfx\\snarfp2.png",
        "gfx\\teleport.png",
        "snd\\door.wav",
        "snd\\empty.wav",
        "snd\\exp.wav",
        "snd\\finish.wav",
        "snd\\firstaid.wav",
        "snd\\hit.wav",
        "snd\\pick.wav",
        "snd\\teleport.wav"
    };

    [STAThread]
    static void Main(string[] args)
    {
        int i;
        for (i = 0; i < flist.Length; i++)
        {
            if (!File.Exists(flist[i]))
            {
                MessageBox.Show(flist[i] + " file is missing! Exiting.", Form1.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }
        if (args.Length == 0 || !int.TryParse(args[0], out i)) i = 1;
        Application.Run(new Form1(i));
    }
}