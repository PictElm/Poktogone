﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;

using Poktogone.Pokemon;
using Poktogone.Battle;

namespace Poktogone.Main
{
    class Program
    {
        static private SqlHelper dbo; // Ued to query to the local Pokemon DB (the .mdf file).
        static private bool isFromCmd; // Used to know weather called with or without arguments.
        static private Random rng; // Grlobal RNG, private; to get RNG, usethe `RngNext` familly of functions.

        /// <summary>
        /// Play a battle between the trainers defined by thir names and sets if given through args,
        /// else ask for the names through stdin and use randomly choosen sets from the database.
        /// If the battle succed to start, process to play a game: get players commands, play turn, resets status.
        /// </summary>
        /// <param name="args">`Poktogone.exe nameP1 teamP1 nameP2 teamP2 [--dbo fileName] [--rng seed] [--dmc (damage calculator args)] [--log [fileName]]`.</param>
        /// <returns>0 if success, else last known <see cref="BattleState"/> (as an int).</returns>
        static int Main(String[] args)
        {
            Program.dbo = new SqlHelper();
            Program.isFromCmd = 0 < args.Length;
            Program.rng = new Random();

            // TODO: args parse (meh..)

            Program.Log("info", "Connecting to sqllocaldb");
            Program.dbo.Connect(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location), "Database.mdf"));
            Program.Log("info", "\tConnected!");

            Trainer P1, P2;
            Battle.Battle battle;

            Program.Log("info", "Loading players' sets");
            if (Program.isFromCmd)
            {
                P1 = new Trainer(args[0], ParseSets(args[1]));
                P2 = new Trainer(args[2], ParseSets(args[3]));
            }
            else
            {
                P1 = new Trainer(Program.Input("Nom du joueur 1: "), ParseSets($"{Program.rng.Next(121) + 1};{Program.rng.Next(121) + 1};{Program.rng.Next(121) + 1}"));
                P2 = new Trainer(Program.Input("Nom du joueur 2: "), ParseSets($"{Program.rng.Next(121) + 1};{Program.rng.Next(121) + 1};{Program.rng.Next(121) + 1}"));
            }
            Program.Log("info", "\tLoaded!");

            battle = new Battle.Battle(P1, P2);
            Program.Println(battle);

            Program.Input("Press Enter to start... ");
            Program.ConsoleClear();

            if (battle.Start())
            {
                do
                {
                    Program.Println(battle);
                    int code = battle.InputCommand(int.Parse(Program.Input("Your player num: ")), Program.Input("Your commande: "));

                    if (code < 0)
                        Program.Println("Wrong player number! (nice try tho...)");
                    else if (code == 0)
                        Program.Println("Wrong command name! (or typo...)");

                    Program.Input("Press Enter to start... ");
                    Program.ConsoleClear();
                }
                while (battle.State != BattleState.VictoryP1 || battle.State != BattleState.VictoryP2);
            }
            else
            {
                Program.Println($"Couln't start battle... Last known state: {battle.State}");
                return (int)battle.State;
            }

            Console.WriteLine(battle.State);
            return 0;
        }

        /// <summary>
        /// Prints to stdout using the <seealso cref="Console"/>.
        /// </summary>
        /// <param name="o">Object to print.</param>
        public static void Print(Object o)
        {
            Console.Write(o.ToString());
        }

        /// <summary>
        /// Prints to stdout using the <seealso cref="Console"/>, then line feeds.
        /// </summary>
        /// <param name="o">Object to print.</param>
        public static void Println(Object o)
        {
            Console.WriteLine(o.ToString());
        }

        /// <summary>
        /// Line feeds.
        /// </summary>
        public static void Println()
        {
            Console.WriteLine();
        }

        /// <summary>
        /// Prints to stdout then waits for an input from stdin using the <seealso cref="Console"/>.
        /// </summary>
        /// <param name="o">Object to print.</param>
        /// <returns>The String read.</returns>
        public static String Input(Object o)
        {
            Program.Print(o);
            return Console.ReadLine();
        }

        /// <summary>
        /// Waits for an input from stdin using the <seealso cref="Console"/>.
        /// </summary>
        /// <returns>The String read.</returns>
        public static String Input()
        {
            return Console.ReadLine();
        }

        /// <summary>
        /// Clear the <seealso cref="Console"/>.
        /// </summary>
        /// <remarks>Only if no args where proviede at start (<seealso cref="Program.isFromCmd"/>).</remarks>
        public static void ConsoleClear()
        {
            if (!Program.isFromCmd)
                Console.Clear();
        }

        /// <summary>
        /// Prints to the log (can be specified through the args, default to the <seealso cref="Console"/>).
        /// </summary>
        /// <param name="tag">Tag to display before the message</param>
        /// <param name="c">Text to display </param>
        public static void Log(String tag, String c)
        {
            // TOD: use the specified output (should be a file), if any.
            Console.WriteLine($"[log]{tag}: {c}");
        }

        /// <summary>
        /// Return the next pseudo random int value, from 0 (included).
        /// </summary>
        /// <param name="maxValue">Max value (excluded).</param>
        /// <returns>A pseudo random, uniformly distributed, int.</returns>
        public static int RngNext(int maxValue)
        {
            return Program.rng.Next(maxValue);
        }

        /// <summary>
        /// Return the next pseudo random int value.
        /// </summary>
        /// <param name="minValue">Min value (included).</param>
        /// <param name="maxValue">Max value (excluded).</param>
        /// <returns>A pseudo random, uniformly distributed, int.</returns>
        public static int RngNext(int minValue, int maxValue)
        {
            return Program.rng.Next(minValue, maxValue);
        }

        /// <summary>
        /// Parse and load from database the 3 sets specified by the argument.
        /// </summary>
        /// <param name="arg">"[setId1];[setId2];[setId3]"</param>
        /// <param name="sep">Separator, use ';' by default</param>
        /// <returns>Return a list of 3 sets.</returns>
        static Set[] ParseSets(String arg, char sep = ';')
        {
            Set[] r = new Set[3];
            int k = 0;

            foreach (String id in arg.Split(sep))
                r[k++] = Set.FromDB(Program.dbo, int.Parse(id));

            return r;
        }

        /// <summary>
        /// Calculate and apply the damage of a move from an attacking pokémon, to the defending pokémon of the defending trainer,
        /// while accounding for the stage's weather, terrain [and other...].
        /// </summary>
        /// <param name="stage">Context for the actions.</param>
        /// <param name="atk">Attacking pokémon.</param>
        /// <param name="def">Defending pokémon.</param>
        /// <param name="defTrainer">Trainer of the defending pokémon.</param>
        /// <returns>The damage inflicted, in percents.</returns>
        /// <remarks>Function signature may change!</remarks>
        public static int DamageCalculator(Stage stage, Set atk, Set def, Trainer defTrainer)
        {
            return 0; /*-*/
        }
    }
}
