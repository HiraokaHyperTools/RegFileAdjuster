using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace RegFileAdjuster {
    class Program {
        static void Main(string[] args) {
            if (args.Length < 2) {
                Console.Error.WriteLine("RegFileAdjuster input.reg output.reg");
                Environment.ExitCode = 1;
                return;
            }
            var fpIn = args[0];
            var fpOut = args[1];
            using (StreamReader reader = new StreamReader(fpIn, Encoding.Unicode)) {
                using (StreamWriter writer = new StreamWriter(fpOut, false, Encoding.Unicode)) {
                    String row = reader.ReadLine();
                    if (row == null || row != "Windows Registry Editor Version 5.00") {
                        Environment.ExitCode = 1;
                        return;
                    }
                    FixEmitter emitter = new FixEmitter(writer);
                    String entire = reader.ReadToEnd();
                    Token token = new Token(entire);
                    while (true) {
                        if (token.EOF)
                            break;
                        if (token.IsNext('[')) {
                            String keyRow = token.ReadConcatLine(false);
                            if (keyRow.EndsWith("]")) {
                                emitter.OpenKey(keyRow.Substring(1, keyRow.Length - 2));
                            }
                            continue;
                        }
                        else {
                            if (!emitter.ValueWith(token)) {
                                token.ReadConcatLine(false);
                            }
                        }
                    }
                }
            }
        }

        class Token {
            public String entire;
            public int x = 0;
            public int cx;

            public Token(String s) {
                entire = s;
                cx = s.Length;
            }

            public string ReadConcatLine(bool crlf) {
                String line = "";
                while (x < cx) {
                    if (entire[x] == '\r') {
                        x++;
                        if (x < cx && entire[x] == '\n') {
                            x++;
                        }
                        break;
                    }
                    else if (entire[x] == '\n') {
                        x++;
                        break;
                    }
                    else if (entire[x] == '\\' && x + 1 < cx && (entire[x + 1] == '\r' || entire[x + 1] == '\n')) {
                        x++;
                        if (x < cx && entire[x] == '\r') {
                            if (crlf) line += '\r';
                            x++;
                            if (x < cx && entire[x] == '\n') {
                                if (crlf) line += '\n';
                                x++;
                            }
                        }
                        else {
                            // \n
                            if (crlf) line += '\n';
                            x++;
                        }
                    }
                    else {
                        line += entire[x];
                        x++;
                    }
                }
                return line;
            }

            public string ReadStr() {
                String str = "";
                if (x >= cx || entire[x] != '"')
                    return null;
                x++;
                while (true) {
                    if (x >= cx)
                        return null;
                    if (entire[x] == '\\') {
                        x++;
                        if (x >= cx)
                            return null;
                        str += entire[x];
                        x++;
                    }
                    else if (entire[x] == '"') {
                        x++;
                        break;
                    }
                    else {
                        str += entire[x];
                        x++;
                    }
                }
                return str;
            }

            public bool EOF { get { return x >= cx; } }

            public bool IsNext(char c) {
                return !EOF && entire[x] == c;
            }

            public bool Eat(char c) {
                if (!EOF && entire[x] == c) {
                    x++;
                    return true;
                }
                return false;
            }
        }

        class FixEmitter {
            TextWriter writer;

            public FixEmitter(TextWriter writer) {
                this.writer = writer;

                writer.WriteLine("Windows Registry Editor Version 5.00");
            }

            internal void OpenKey(string key) {
                writer.WriteLine();
                writer.WriteLine("[{0}]", key);
            }

            internal static String Escape(String s) {
                return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
            }


            internal bool ValueWith(Token token) {
                String left = token.ReadStr();
                if (left == null)
                    return false;
                if (!token.Eat('='))
                    return false;
                if (token.IsNext('"')) {
                    String str = token.ReadStr();
                    StringBuilder builder = new StringBuilder();
                    foreach (byte b in Encoding.Unicode.GetBytes(str)) {
                        builder.AppendFormat("{0:X2},", b);
                    }
                    writer.WriteLine("\"{0}\"=hex(1):{1}", Escape(left), builder.ToString() + "00,00");
                }
                else {
                    writer.WriteLine("\"{0}\"={1}", Escape(left), token.ReadConcatLine(false));
                }
                return true;
            }
        }
    }
}
