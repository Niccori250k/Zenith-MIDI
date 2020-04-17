﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using ZenithEngine;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using ZenithEngine.UI;

namespace NoteCountRenderMod
{
    public class Render : IPluginRender
    {
        #region PreviewConvert
        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
        #endregion

        public string Name => "Note Counter Mod";

        public string Description => "Generate note counts and other midi statistics.\nMod version by Niccori 250k";

        public string LanguageDictName { get; } = "notecounter";

        public bool Initialized { get; set; } = false;

        public System.Windows.Media.ImageSource PreviewImage { get; set; }

        public bool ManualNoteDelete => true;

        public double NoteCollectorOffset => 0;

        public double Tempo { get; set; }

        public NoteColor[][] NoteColors { get; set; }

        public MidiInfo CurrentMidi { get; set; }

        public double NoteScreenTime => 0;

        public long LastNoteCount { get; set; } = 0;

        public System.Windows.Controls.Control SettingsControl { get; set; }

        RenderSettings renderSettings;
        Settings settings;
        //NumberSelect NumberSelect;

        GLTextEngine textEngine;
        public void Dispose()
        {
            textEngine.Dispose();
            Initialized = false;
            if (outputCsv != null) outputCsv.Close();
            Console.WriteLine("Disposed of NoteCountRender");
        }

        int fontSize = 48;
        string font = "MS UI Gothic";
        public System.Drawing.FontStyle fontStyle = System.Drawing.FontStyle.Regular;

        StreamWriter outputCsv = null;

        public void Init()
        {
            textEngine = new GLTextEngine();
            if (settings.fontName != font || settings.fontSize != fontSize || settings.fontStyle != fontStyle)
            {
                font = settings.fontName;
                fontSize = settings.fontSize;
                fontStyle = settings.fontStyle;
            }
            textEngine.SetFont(font, fontStyle, fontSize);
            noteCount = 0;
            Mplph = 0;
            nps = 0;
            Mnps = 0;
            //npq = 0;
            //Mnpq = 0;
            frames = 0;
            notesHit = new LinkedList<long>();
            //Array.Resize(ref ncl, CurrentMidi.division);
            Initialized = true;

            if (settings.saveCsv && settings.csvOutput != "")
            {
                outputCsv = new StreamWriter(settings.csvOutput);
            }

            Console.WriteLine("Initialised NoteCountRender");
        }

        public Render(RenderSettings settings)
        {
            this.renderSettings = settings;
            this.settings = new Settings();
            SettingsControl = new SettingsCtrl(this.settings);
            PreviewImage = BitmapToImageSource(Properties.Resources.preview);
        }

        long noteCount = 0;
        double nps = 0;
        double Mnps = 0;
        int frames = 0;
        long currentNotes = 0;
        long polyphony = 0;
        long Mplph = 0;
        //long npq = 0;
        //long Mnpq = 0;
        //long[] ncl = new long[0];
        
        LinkedList<long> notesHit = new LinkedList<long>();

        public void RenderFrame(FastList<Note> notes, double midiTime, int finalCompositeBuff)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, finalCompositeBuff);

            GL.Viewport(0, 0, renderSettings.width, renderSettings.height);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.Clear(ClearBufferMask.DepthBufferBit);

            GL.Enable(EnableCap.Blend);
            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.ColorArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.Enable(EnableCap.Texture2D);

            GL.EnableVertexAttribArray(0);
            GL.EnableVertexAttribArray(1);

            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            if (settings.fontName != font || settings.fontSize != fontSize || settings.fontStyle != fontStyle)
            {
                font = settings.fontName;
                fontSize = settings.fontSize;
                fontStyle = settings.fontStyle;
                textEngine.SetFont(font, fontStyle, fontSize);
            }
            if (!renderSettings.Paused)
            {
                polyphony = 0;
                currentNotes = 0;
                long nc = 0;
                lock (notes)
                    foreach (Note n in notes)
                    {
                        nc++;
                        if (n.start < midiTime)
                        {
                            if (n.end > midiTime || !n.hasEnded)
                            {
                                polyphony++;
                                if (Mplph < polyphony) Mplph = polyphony;
                            }
                            else if (n.meta != null)
                            {
                                n.delete = true;
                            }
                            if (n.meta == null)
                            {
                                currentNotes++;
                                noteCount++;
                                n.meta = true;
                            }
                        }
                        if (n.start > midiTime) break;
                    }
                LastNoteCount = nc;
                notesHit.AddLast(currentNotes);
                while (notesHit.Count > renderSettings.fps) notesHit.RemoveFirst();
                nps = notesHit.Sum();
                if (Mnps < nps) Mnps = nps;
                /*ncl[(int)((long)midiTime % CurrentMidi.division)] = noteCount;
                if ((long)midiTime % CurrentMidi.division == CurrentMidi.division - 1)
                {
                    npq = ncl[CurrentMidi.division - 1] - ncl[0];
                }
                else
                {
                    npq = ncl[(int)((long)midiTime % CurrentMidi.division)] - ncl[(int)((long)midiTime % CurrentMidi.division + 1)];
                }
                if (Mnpq < npq) Mnpq = npq;*/
            }

            double tempo = Tempo;

            int seconds = (int)Math.Floor((double)frames  * 10 / renderSettings.fps) / 10;
            double dseconds = Math.Floor((double)frames  * 10 / renderSettings.fps) / 10;
            int milliseconds = (int)Math.Floor((double)frames * 1000 / renderSettings.fps);
            int totalsec = (int)Math.Floor(CurrentMidi.secondsLength);
            //double totaldsec = CurrentMidi.secondsLength;
            int totalframes = (int)Math.Ceiling(CurrentMidi.secondsLength * renderSettings.fps);
            //int totalmsec = (int)Math.Floor(CurrentMidi.millisecondsLength);
            if (seconds > totalsec) seconds = totalsec;
            TimeSpan time = new TimeSpan(0, 0, seconds);
            TimeSpan miltime = new TimeSpan(0, 0, 0, 0, milliseconds);
            TimeSpan totaltime = new TimeSpan(0, 0, totalsec);
            if (time > totaltime) time = totaltime;
            if (!renderSettings.Paused) frames++;
            if (frames > totalframes) frames = totalframes;

            double barDivide = (double)CurrentMidi.division * CurrentMidi.timeSig.numerator / CurrentMidi.timeSig.denominator * 4;

            long limMidiTime = (long)midiTime;
            if (limMidiTime > CurrentMidi.tickLength) limMidiTime = CurrentMidi.tickLength;

            long bar = (long)Math.Floor(limMidiTime / barDivide) + 1;
            long maxbar = (long)Math.Floor(CurrentMidi.tickLength / barDivide);
            if (bar > maxbar) bar = maxbar;

            Func<string, Commas, string> replace = (text, separator) =>
            {
                string sep = "";
                TimeSpan totalmiltime = new TimeSpan(0, 0, 0, totalsec, 0);
                string totaldsec = totalsec.ToString(sep + "0");
                int bpmdigits = 2;
                string digits = "";
                string bpmzp = "0.00";
                string nczp = "0";
                string barzp = "0";
                if (Regex.IsMatch(text, @"{tmiltime}\.(\d{3})[^\d]"))
                {
                    Match match = Regex.Match(text, @"{tmiltime}\.(\d{3})[^\d]");
                    totalmiltime = new TimeSpan(0, 0, 0, totalsec, int.Parse(match.Groups[1].Value));
                }
                if (Regex.IsMatch(text, @"{totalsec}\.(\d)[^\d]"))
                {
                    Match match = Regex.Match(text, @"{totalsec}\.(\d)[^\d]");
                    totaldsec = totaldsec + "." + match.Groups[1].Value;
                }
                if (Regex.IsMatch(text, @"{bpm}(\d+)"))
                {
                    Match match = Regex.Match(text, @"{bpm}(\d+)");
                    bpmdigits = int.Parse(match.Groups[1].Value);
                    if (bpmdigits > 12) bpmdigits = 12;
                }
                double tdsec = double.Parse(totaldsec);
                if (separator == Commas.Comma) sep = "#,##";
                if (miltime > totalmiltime) miltime = totalmiltime;
                if (dseconds > tdsec) dseconds = tdsec;
                if (settings.AdditionalZeroes) {
                    digits = "000." + new string('0', bpmdigits);
                    bpmzp = "000.00";
                    nczp = "00000";
                    barzp = "000";
                }
                else {
                    digits = "0." + new string('0', bpmdigits);
                }

                if (bpmdigits == 0) digits = "0";
                text = Regex.Replace(text, @"{bpm}\d+", Math.Round(tempo, bpmdigits, MidpointRounding.AwayFromZero).ToString(digits));
                text = text.Replace("{bpm}", Math.Round(tempo, 2, MidpointRounding.AwayFromZero).ToString(bpmzp));
                text = text.Replace("{truebpm}", tempo.ToString());

                text = text.Replace("{nc}", noteCount.ToString(sep + nczp));
                text = text.Replace("{nr}", (CurrentMidi.noteCount - noteCount).ToString(sep + nczp));
                text = text.Replace("{tn}", CurrentMidi.noteCount.ToString(sep + nczp));


                text = text.Replace("{nps}", Math.Round(nps).ToString(sep + "0"));
                text = text.Replace("{mnps}", Math.Round(Mnps).ToString(sep + "0"));
                text = text.Replace("{plph}", polyphony.ToString(sep + "0"));
                text = text.Replace("{mplph}", Mplph.ToString(sep + "0"));
                //text = text.Replace("{npq}", npq.ToString(sep + "0"));
                //text = text.Replace("{mnpq}", Mnpq.ToString(sep + "0"));

                text = text.Replace("{currsec}", dseconds.ToString(sep + "0.0"));
                text = text.Replace("{currtime}", time.ToString("mm\\:ss"));
                text = text.Replace("{cmiltime}", miltime.ToString("mm\\:ss\\.fff"));
                text = Regex.Replace(text, @"{totalsec}\.\d", totaldsec);
                text = text.Replace("{totaltime}", totaltime.ToString("mm\\:ss"));
                text = text.Replace("{remsec}", (tdsec - dseconds).ToString(sep + "0.0"));
                text = text.Replace("{remtime}", (totaltime - time).ToString("mm\\:ss"));
                text = Regex.Replace(text, @"{tmiltime}\.\d{3}", totalmiltime.ToString("mm\\:ss\\.fff"));
                text = text.Replace("{rmiltime}", (totalmiltime - miltime).ToString("mm\\:ss\\.fff"));
                text = text.Replace("{cftime}", time.ToString("mm\\:ss") + ":" + (frames % renderSettings.fps).ToString("0"));
                text = text.Replace("{tftime}", totaltime.ToString("mm\\:ss") + ":" + (totalframes % renderSettings.fps).ToString("0"));

                text = text.Replace("{currticks}", (limMidiTime).ToString(sep + "0"));
                text = text.Replace("{totalticks}", (CurrentMidi.tickLength).ToString(sep + "0"));
                text = text.Replace("{remticks}", (CurrentMidi.tickLength - limMidiTime).ToString(sep + "0"));

                text = text.Replace("{currbars}", bar.ToString(sep + barzp));
                text = text.Replace("{totalbars}", maxbar.ToString(sep + barzp));
                text = text.Replace("{rembars}", (maxbar - bar).ToString(sep + barzp));

                text = text.Replace("{ppq}", CurrentMidi.division.ToString());
                text = text.Replace("{tsn}", CurrentMidi.timeSig.numerator.ToString());
                text = text.Replace("{tsd}", CurrentMidi.timeSig.denominator.ToString());
                text = text.Replace("{avgnps}", ((double)CurrentMidi.noteCount / (double)totalsec).ToString(sep + "0.00"));
                text = text.Replace("{avgnpq}", ((double)CurrentMidi.noteCount / (double)CurrentMidi.division).ToString(sep + "0.00"));

                text = text.Replace("{currframes}", frames.ToString());
                text = text.Replace("{totalframes}", totalframes.ToString());
                text = text.Replace("{remframes}", (totalframes - frames).ToString());

                text = text.Replace("{np}", (noteCount * 1000000 / CurrentMidi.noteCount).ToString("000000").Insert(2, ".") + "%");
                text = text.Replace("{ticksp}", (limMidiTime * 1000000 / CurrentMidi.tickLength).ToString("000000").Insert(2, ".") + "%");
                text = text.Replace("{timep}", (miltime.TotalMilliseconds * 1000000 / totalmiltime.TotalMilliseconds).ToString("000000").Insert(2, ".") + "%");

                text = text.Replace("{fps}", renderSettings.fps.ToString());
                text = text.Replace("{vwidth}", (renderSettings.width / renderSettings.downscale).ToString());
                text = text.Replace("{vheight}", (renderSettings.height / renderSettings.downscale).ToString());
                return text;
            };


            string renderText = replace(settings.text, settings.thousandSeparator);
            Regex.Replace(renderText, @"\[(.*?)\]", "{" + Regex.Match(renderText, @"\[(.*?)\]").Groups[1].Value + "}");

            if (settings.textAlignment == Alignments.TopLeft)
            {
                var size = textEngine.GetBoundBox(renderText);
                Matrix4 transform = Matrix4.Identity;
                transform = Matrix4.Mult(transform, Matrix4.CreateScale(1.0f / renderSettings.width, -1.0f / renderSettings.height, 1.0f));
                transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(-1, 1, 0));
                transform = Matrix4.Mult(transform, Matrix4.CreateRotationZ(0));

                textEngine.Render(renderText, transform, Color4.White);
            }
            else if (settings.textAlignment == Alignments.TopRight)
            {
                float offset = 0;
                string[] lines = renderText.Split('\n');
                foreach (var line in lines)
                {
                    var size = textEngine.GetBoundBox(line);
                    Matrix4 transform = Matrix4.Identity;
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(-size.Width, offset, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateScale(1.0f / renderSettings.width, -1.0f / renderSettings.height, 1.0f));
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(1, 1, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateRotationZ(0));
                    offset += size.Height;
                    textEngine.Render(line, transform, Color4.White);
                }
            }
            else if (settings.textAlignment == Alignments.BottomLeft)
            {
                float offset = 0;
                string[] lines = renderText.Split('\n');
                foreach (var line in lines.Reverse())
                {
                    var size = textEngine.GetBoundBox(line);
                    Matrix4 transform = Matrix4.Identity;
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(0, offset - size.Height, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateScale(1.0f / renderSettings.width, -1.0f / renderSettings.height, 1.0f));
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(-1, -1, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateRotationZ(0));
                    offset -= size.Height;
                    textEngine.Render(line, transform, Color4.White);
                }
            }
            else if (settings.textAlignment == Alignments.BottomRight)
            {
                float offset = 0;
                string[] lines = renderText.Split('\n');
                foreach (var line in lines.Reverse())
                {
                    var size = textEngine.GetBoundBox(line);
                    Matrix4 transform = Matrix4.Identity;
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(-size.Width, offset - size.Height, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateScale(1.0f / renderSettings.width, -1.0f / renderSettings.height, 1.0f));
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(1, -1, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateRotationZ(0));
                    offset -= size.Height;
                    textEngine.Render(line, transform, Color4.White);
                }
            }
            else if (settings.textAlignment == Alignments.TopSpread)
            {
                float offset = 0;
                string[] lines = renderText.Split('\n');
                float dist = 1.0f / (lines.Length + 1);
                int p = 1;
                foreach (var line in lines.Reverse())
                {
                    var size = textEngine.GetBoundBox(line);
                    Matrix4 transform = Matrix4.Identity;
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(-size.Width / 2, 0, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateScale(1.0f / renderSettings.width, -1.0f / renderSettings.height, 1.0f));
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation((dist * p++) * 2 - 1, 1, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateRotationZ(0));
                    offset -= size.Height;
                    textEngine.Render(line, transform, Color4.White);
                }
            }
            else if (settings.textAlignment == Alignments.BottomSpread)
            {
                float offset = 0;
                string[] lines = renderText.Split('\n');
                float dist = 1.0f / (lines.Length + 1);
                int p = 1;
                foreach (var line in lines.Reverse())
                {
                    var size = textEngine.GetBoundBox(line);
                    Matrix4 transform = Matrix4.Identity;
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation(-size.Width / 2, -size.Height, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateScale(1.0f / renderSettings.width, -1.0f / renderSettings.height, 1.0f));
                    transform = Matrix4.Mult(transform, Matrix4.CreateTranslation((dist * p++) * 2 - 1, -1, 0));
                    transform = Matrix4.Mult(transform, Matrix4.CreateRotationZ(0));
                    offset -= size.Height;
                    textEngine.Render(line, transform, Color4.White);
                }
            }

            if(outputCsv != null)
            {
                outputCsv.WriteLine(replace(settings.csvFormat, Commas.Nothing));
            }

            GL.Disable(EnableCap.Blend);
            GL.Disable(EnableCap.Texture2D);
            GL.DisableClientState(ArrayCap.VertexArray);
            GL.DisableClientState(ArrayCap.ColorArray);
            GL.DisableClientState(ArrayCap.TextureCoordArray);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
        }

        public void ReloadTrackColors()
        {

        }
    }
}
