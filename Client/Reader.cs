using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Client
{
    public class Reader
    {
        static private string Line = "";
        static private string Input = "";
        static private string LastWord = "";
        static private int InputIndex = 0;
        static private int TabIndex = 0;
        static private int HistoryPos = -1;
        static private int StartLeft = 3;
        static private int ConsoleLeft = 0;
        static private List<string> History = new List<string>();

        public delegate void TabPressedDelegate(string word, int iteration);
        static public event TabPressedDelegate TabPressed;

        static public string Read(bool saveInput = true)
        {
            const string allowedInput = "[\\w/\\\"'?!@#$%^&*<>:;.,\\]\\[()-=_+{}~` ]";
            StartLeft = Console.CursorLeft;
            ConsoleLeft = Console.CursorLeft;
            char symbol = ' ';
            Line = "";
            #region symbolReading
            while (symbol != '\r' || Line == "")
            {
                //Read symbol
                ConsoleKeyInfo key = Console.ReadKey(true);
                symbol = key.KeyChar;

                if (key.Key != ConsoleKey.Tab) TabIndex = 0;
                if (symbol == '\r' || symbol == '\n') break;

                //Treat special keys
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (Line != "" && InputIndex > 0)
                    {
                        Line = Line.Remove(InputIndex - 1, 1);
                        ConsoleLeft = Console.CursorLeft;
                        RenderText(Line);
                        InputIndex--;
                    }
                }
                else if (key.Key == ConsoleKey.UpArrow)
                {
                    if (HistoryPos > 0)
                    {
                        HistoryPos--;
                        Line = History[HistoryPos];
                        ConsoleLeft = Console.CursorLeft;
                        RenderText(Line);
                        InputIndex = Line.Length;
                        Console.CursorLeft = Line.Length + StartLeft;
                        ConsoleLeft = Console.CursorLeft;
                    }
                }
                else if (key.Key == ConsoleKey.DownArrow)
                {
                    if (HistoryPos < History.Count - 1)
                    {
                        HistoryPos++;
                        Line = History[HistoryPos];
                        ConsoleLeft = Console.CursorLeft;
                        RenderText(Line);
                        InputIndex = Line.Length;
                        Console.CursorLeft = Line.Length + StartLeft;
                        // ConsoleLeft = Line.Length + StartLeft;
                    }
                }
                else if (key.Key == ConsoleKey.Delete)
                {
                    if (InputIndex < Line.Length)
                    {
                        Line = Line.Remove(InputIndex, 1);
                        ConsoleLeft = Console.CursorLeft;
                        if (RenderText(Line) != -1)
                            Console.CursorLeft++;
                    }
                }
                else if (key.Key == ConsoleKey.LeftArrow)
                {
                    if (InputIndex > 0)
                    {
                        InputIndex--;
                        if (Console.CursorLeft > 0)
                            Console.CursorLeft--;
                    }
                }
                else if (key.Key == ConsoleKey.RightArrow)
                {
                    if (InputIndex < Input.Length)
                    {
                        InputIndex++;
                        Console.CursorLeft++;
                    }
                }
                else if (key.Key == ConsoleKey.Home)
                {
                    Console.CursorLeft -= InputIndex;
                    InputIndex = 0;
                }
                else if (key.Key == ConsoleKey.End)
                {
                    if ((Console.CursorLeft + Input.Length - InputIndex) >= 0)
                        Console.CursorLeft += Input.Length - InputIndex;
                    InputIndex = Input.Length;
                }
                else if (key.Key == ConsoleKey.Tab && Console.CursorLeft < Console.BufferWidth - 4)
                {
                    TabPressed?.Invoke(LastWord, TabIndex);
                    TabIndex++;
                }
                else if (Regex.IsMatch(symbol.ToString(), allowedInput) && Console.CursorLeft < Console.BufferWidth - 4)
                {
                    Line = Line.Insert(InputIndex, symbol.ToString());
                    ConsoleLeft = Console.CursorLeft;
                    RenderText(Line);
                    InputIndex++;
                }

                ConsoleLeft = Console.CursorLeft;
                if (key.Key != ConsoleKey.Tab)
                {
                    if (Input.Split(' ').Length > 0)
                    {
                        LastWord = Input.Split(' ')[Input.Split(' ').Length - 1];
                    }
                    else
                    {
                        LastWord = "";
                    }
                }
            }
            #endregion
            if (saveInput || (Line.StartsWith("/") && !Line.StartsWith("/w")))
                Console.WriteLine();
            Input = "";
            LastWord = "";
            InputIndex = 0;
            StartLeft = 3;
            ConsoleLeft = Console.CursorLeft;

            if (Line != "" && (History.Count == 0 || Line != History[History.Count - 1]))
                History.Add(Line);
            HistoryPos = History.Count;
            return Line;
        }

        static public void Update()
        {
            Console.Write(Input);
            ConsoleLeft += Input.Length;
            StartLeft = 3;
        }

        static public void InsertWord(string word)
        {
            if (StartLeft + word.Length < Console.BufferWidth - 4)
            {
                if (Line.Length > 0)
                {
                    string[] words = Line.Split(' ');
                    words[words.Length - 1] = word;
                    Line = string.Join(" ", words);
                }
                else
                {
                    Line = word;
                }
                RenderText(Line);
                InputIndex = Line.Length;
            }
        }

        static int RenderText(string text)
        {
            int left = ConsoleLeft - InputIndex;
            if (left < 0)
            {
                Console.CursorLeft = 0;
                ConsoleLeft = 0;
            }
            else
            {
                Console.CursorLeft = left;
                ConsoleLeft = left;
            }

            Console.Write(text);
            ConsoleLeft += text.Length;

            int clearCount = Console.BufferWidth - ConsoleLeft - 2;
            Console.Write(String.Concat(Enumerable.Repeat(" ", clearCount)));
            left = ConsoleLeft - (Input.Length - InputIndex);
            int trueLeft = left;
            if (left < 0)
                left = 0;
            Console.CursorLeft = left;
            ConsoleLeft = left;
            Input = text;
            return trueLeft;
        }
    }
}