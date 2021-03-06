﻿using System;
using System.Collections.Generic;
using System.IO;
using Pulsus.Graphics;

namespace Pulsus.Gameplay
{
	public class Skin : EventPlayer
	{
		class LaneObject
		{
			public static LaneObject CreateNote(int lane, NoteEvent noteEvent, double position)
			{
				return new LaneObject(noteEvent, position);
			}

			public static LaneObject CreateLongNote(int lane, LongNoteEvent noteEvent, double position, double positionEnd)
			{
				return new LaneObject(noteEvent, position, positionEnd);
			}

			public static LaneObject CreateMeasureMarker(MeasureMarkerEvent markerEvent, double position)
			{
				return new LaneObject(markerEvent, position);
			}

			private LaneObject(Event laneEvent, double position)
			{
				this.laneEvent = laneEvent;
				this.position = position;
				this.positionEnd = position;
			}

			private LaneObject(Event laneEvent, double position, double positionEnd)
			{
				this.laneEvent = laneEvent;
				this.position = position;
				this.positionEnd = positionEnd;
			}

			public Event laneEvent;
			public double position;
			public double positionEnd;
		}

		double timer = 0.0;
		public double baseScrollTime = 0.0;
		public double scrollTime { get { return baseScrollTime; } }
		double baseBpm = 0.0;
		int lastScrollEvent = 0;

		const bool useInterpolation = true;
		const double pressKeyFadeInTime = 0;
		const double pressKeyFadeOutTime = 0.12;
		const double pressKeyMaxThreshold = pressKeyFadeOutTime * 0.6;
		const double pressLaneFadeInTime = 0;
		const double pressLaneFadeOutTime = 0.15;

		int keyCount = 0;
		int playerCount = 0;
		int[] laneWidths = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };

		// missed notes will stick around on judge line for a while
		const bool hideMissedNotes = false;

		private List<LaneObject> laneObjects = new List<LaneObject>(100);
		private double[] laneLastPress;
		private int[] laneActive;

		private Renderer renderer;
		private BMSJudge judge;

		Texture2D textureLaneBG1;
		Texture2D textureLaneBG2;
		Texture2D textureLaneBG3;
		Texture2D textureNotes;
		SubTexture textureNote1;
		SubTexture textureNote2;
		SubTexture textureNote3;
		SubTexture textureNote1LN;
		SubTexture textureNote2LN;
		SubTexture textureNote3LN;
		Texture2D textureKeyW;
		Texture2D textureKeyB;
		Texture2D textureKeyNormal;
		Texture2D textureKeyWide;
		Texture2D textureLanePress1;
		Texture2D textureLanePress2;
		Texture2D textureLanePress3;
		Texture2D textureTTBG;
		Texture2D textureTTRotator;
		Texture2D textureLaneGlow;
		Texture2D textureProgressBG;
		Texture2D textureProgressBar;
		Texture2D textureNoteHit;
		SubTexture[] textureNoteHitFrames;
		Texture2D textureGaugeTick;
		Texture2D textureGaugeTickOff;
		Texture2D textureGaugeTickTop;
		Texture2D textureGaugeTickTopOff;

		Font judgeFont = null;
		string judgeStr = "";
		JudgeText judgeText = JudgeText.Empty;
		int judgeNumber = 0;
		double judgePrintTime = 1.0;
		double judgePrintTimer = 0.0;
		double judgeAnimationTimer = 0.0;
		double judgeAnimationFrametime = 1.0 / 30.0;
		double judgeTextY = 0;
		Rectangle judgeTextRect = new Rectangle(0, 0, 153, 56);
		Rectangle judgeNumberRect = new Rectangle(154, 0, 37, 56);
		float judgePosOffset = 0.0f;
		int judgeAnimationIndex = 0;

		Color[] judgeTextColors = new Color[]
		{
			new Color(230, 64, 0, 255),		// Poor
			new Color(230, 100, 0, 255),	// Bad
			new Color(255, 196, 0, 255),	// Good/Great
			new Color(230, 176, 176, 255),	// PGreat #1
			new Color(176, 230, 176, 255),	// PGreat #2
			new Color(176, 176, 230, 255),	// PGreat #3
		};

		// note hit animation
		int[] noteHitFrames;
		double[] noteHitFrameTimers;
		double noteHitFrametime = 1.0 / 120.0;

		int gaugeTickState = 1;
		double gaugeTickTimer;
		double gaugeTickFrametime = 1.0 / (0.175 * 120.0);

		public bool disableBGA;

		public enum JudgeText : int
		{
			Empty,
			PGreat,
			Great,
			Good,
			Bad,
			Poor,
		};

		class BGAImage
		{
			public BGAImage(Texture2D texture, Color color, Color transparent)
			{
				this.texture = texture;
				this.color = color;
				this.transparent = transparent;
			}

			public BGAObject bga;
			public Texture2D texture;
			public Color color;
			public Color transparent;
			public double frametimer;
		}

		Texture2D bgaRTTexture;
		FrameBuffer bgaFramebuffer;
		BGAImage bgaBase;
		BGAImage bgaLayerKeyed;
		BGAImage bgaLayer;
		BGAImage bgaPoor;

		public Skin(Chart chart, Renderer renderer, BMSJudge judge)
			: base(chart)
		{
			this.renderer = renderer;
			this.judge = judge;

			bgaBase = new BGAImage(null, Color.Black, Color.Black);
			bgaLayerKeyed = new BGAImage(null, Color.Black, Color.Black);
			bgaLayer = new BGAImage(null, Color.Black, Color.Black);
			bgaPoor = new BGAImage(null, Color.Black, Color.Black);

			string skinPath = Path.Combine(Program.basePath, "Skins", SettingsManager.instance.skin);
			string gfxPath = Path.Combine(skinPath, "gfx" + Path.DirectorySeparatorChar);
			string fontPath = Path.Combine(skinPath, "fonts");

			textureNotes = new Texture2D(gfxPath + "Notes.png");
			textureNote1 = new SubTexture(textureNotes, new Rectangle(0, 0, 34, 8));
			textureNote2 = new SubTexture(textureNotes, new Rectangle(34, 0, 30, 8));
			textureNote3 = new SubTexture(textureNotes, new Rectangle(0, 16, 54, 8));
			textureNote1LN = new SubTexture(textureNotes, new Rectangle(0, 8, 34, 8));
			textureNote2LN = new SubTexture(textureNotes, new Rectangle(34, 8, 30, 8));
			textureNote3LN = new SubTexture(textureNotes, new Rectangle(0, 24, 54, 8));
			textureLaneBG1 = new Texture2D(gfxPath + "LaneBG1.png");
			textureLaneBG2 = new Texture2D(gfxPath + "LaneBG2.png");
			textureLaneBG3 = new Texture2D(gfxPath + "LaneBG3.png");
			textureKeyW = new Texture2D(gfxPath + "KeyW.png");
			textureKeyB = new Texture2D(gfxPath + "KeyB.png");
			textureKeyNormal = new Texture2D(gfxPath + "KeyNormal.png");
			textureKeyWide = new Texture2D(gfxPath + "KeyWide.png");
			textureLanePress1 = new Texture2D(gfxPath + "LanePress1.png");
			textureLanePress2 = new Texture2D(gfxPath + "LanePress2.png");
			textureLanePress3 = new Texture2D(gfxPath + "LanePress3.png");
			textureTTBG = new Texture2D(gfxPath + "TTBackground.png");
			textureTTRotator = new Texture2D(gfxPath + "TTRotator.png");
			textureLaneGlow = new Texture2D(gfxPath + "LaneGlow.png");
			textureProgressBG = new Texture2D(gfxPath + "ProgressBG.png");
			textureProgressBar = new Texture2D(gfxPath + "ProgressBar.png");
			textureNoteHit = new Texture2D(gfxPath + "NoteHit.png");
			textureNoteHitFrames = TextureAtlas.CreateFromGrid(textureNoteHit, 200, 200, 10);
			textureGaugeTick = new Texture2D(gfxPath + "GaugeTick.png");
			textureGaugeTickOff = new Texture2D(gfxPath + "GaugeTickOff.png");
			textureGaugeTickTop = new Texture2D(gfxPath + "GaugeTickTop.png");
			textureGaugeTickTopOff = new Texture2D(gfxPath + "GaugeTickTopOff.png");

			playerCount = chart.players;
			keyCount = chart.playerChannels;

			if (keyCount == 6 || // 5 keys + TT
				keyCount == 8)   // 7 keys + TT
			{
				laneWidths[0] = textureNote3.width;
				for (int i = 1; i < keyCount; ++i)
					laneWidths[i] = (i % 2 == 1) ? textureNote1.width : textureNote2.width;
			}
			else // 9 keys and others
			{
				for (int i = 0; i < keyCount; ++i)
					laneWidths[i] = (i % 2 == 0) ? textureNote1.width : textureNote2.width;
			}

			noteHitFrames = new int[playerCount * keyCount];
			noteHitFrameTimers = new double[playerCount * keyCount];
			laneLastPress = new double[playerCount * keyCount];
			laneActive = new int[playerCount * keyCount];
			for (int i = 0; i < playerCount * keyCount; i++)
			{
				noteHitFrames[i] = -1;
				laneLastPress[i] = double.MinValue;
			}

			Settings settings = SettingsManager.instance;
			baseScrollTime = settings.gameplay.scrollTime;
			disableBGA = settings.gameplay.disableBGA;
			judgeTextY = settings.gameplay.judgePositionY;

			baseBpm = chart.bpm;

			judgeFont = new Font(Path.Combine(fontPath, "HNkani.ttf"),
				46, FontStyle.Normal, false);
		}

		public override void Dispose()
		{
			base.Dispose();

			textureLaneBG1.Dispose();
			textureLaneBG2.Dispose();
			textureLaneBG3.Dispose();
			textureNotes.Dispose();
			textureKeyW.Dispose();
			textureKeyB.Dispose();
			textureKeyNormal.Dispose();
			textureKeyWide.Dispose();
			textureLanePress1.Dispose();
			textureLanePress2.Dispose();
			textureLanePress3.Dispose();
			textureTTBG.Dispose();
			textureTTRotator.Dispose();
			textureLaneGlow.Dispose();
			textureProgressBG.Dispose();
			textureProgressBar.Dispose();
			textureNoteHit.Dispose();
			textureGaugeTick.Dispose();
			textureGaugeTickOff.Dispose();
			textureGaugeTickTop.Dispose();
			textureGaugeTickTopOff.Dispose();

			if (bgaFramebuffer != null)
				bgaFramebuffer.Dispose();
			if (bgaRTTexture != null)
				bgaRTTexture.Dispose();

			judgeFont.Dispose();
		}

		public override void OnPlayerStart()
		{
			lastScrollEvent = lastEventIndex;
		}

		public void OnKeyPress(int lane)
		{
			laneLastPress[lane] = timer;
			laneActive[lane]++;
		}

		public void OnKeyRelease(int lane)
		{
			laneLastPress[lane] = timer;
			laneActive[lane]--;
		}

		public void OnNoteJudged(NoteScore noteScore)
		{
			if (Math.Abs(noteScore.hitOffset) <= judge.timingWindow[0])
			{
				PrintJudge(JudgeText.PGreat, judge.combo);
				noteHitFrames[noteScore.noteEvent.lane] = 0;
			}
			else if (Math.Abs(noteScore.hitOffset) <= judge.timingWindow[1])
				PrintJudge(JudgeText.Great, judge.combo);
			else if (Math.Abs(noteScore.hitOffset) <= judge.timingWindow[2])
				PrintJudge(JudgeText.Good, judge.combo);
			else if (Math.Abs(noteScore.hitOffset) <= judge.timingWindow[3])
				PrintJudge(JudgeText.Bad, judge.combo);
			else
				PrintJudge(JudgeText.Poor, judge.combo);
		}

		public override void OnBGA(BGAEvent bgaEvent)
		{
			if (bgaEvent.bga == null)
				return;

			if (disableBGA)
				return;

			BGAObject bga = bgaEvent.bga;
			Texture2D texture = null;
			Color color = Color.Black;

			if (bga != null)
			{
				texture = bga.texture;

				if (texture != null)
					color = Color.White;
				else
					Log.Warning("Failed to show BGA object: " + bgaEvent.bga.name);

				bga.Start();
			}

			if (bgaEvent.type == BGAEvent.BGAType.BGA)
			{
				bgaBase.bga = bga;
				bgaBase.texture = texture;
				bgaBase.color = color;
			}
			else if (bgaEvent.type == BGAEvent.BGAType.Poor)
			{
				bgaPoor.texture = texture;
				bgaPoor.color = color;
			}
			else if (bgaEvent.type == BGAEvent.BGAType.LayerTransparentBlack)
			{
				bgaLayerKeyed.texture = texture;
				bgaLayerKeyed.color = color;
			}
			else if (bgaEvent.type == BGAEvent.BGAType.Layer)
			{
				bgaLayer.texture = texture;
				bgaLayer.color = color;
			}

			bgaBase.frametimer = 0.0;
		}

		public override void Update(double deltaTime)
		{
			timer += deltaTime;
			if (bgaBase.bga != null)
				bgaBase.bga.Update(deltaTime);

			base.Update(deltaTime);
		}

		private void UpdateLaneNotes()
		{
			laneObjects.Clear();

			if (!playing)
				return;

			int activeLongNote = int.MaxValue;
			int firstVisibleNote = lastScrollEvent;
			double laneBpm = bpm;

			double lastTimestamp = currentTime;
			double currentScrollTime = Math.Abs((scrollTime) * (baseBpm / laneBpm));
			double relPosition = 0.0;

			for (int index = lastScrollEvent; index < eventList.Count; index++)
			{
				Event currentEvent = eventList[index];
				if (currentEvent.pulse < pulse)
				{
					NoteEvent noteEvent = currentEvent as NoteEvent;
					LongNoteEvent longNoteEvent = currentEvent as LongNoteEvent;

					if (noteEvent == null ||
						(longNoteEvent != null && longNoteEvent.endNote.pulse < pulse))
					{
						firstVisibleNote = index + 1;
						continue;
					}
				}

				if (currentEvent is BPMEvent)
				{
					double timestamp = currentEvent.timestamp;
					double delta = timestamp - lastTimestamp;

					relPosition += delta / currentScrollTime;

					laneBpm = (currentEvent as BPMEvent).bpm;
					currentScrollTime = Math.Abs((scrollTime) * (baseBpm / laneBpm));

					lastTimestamp = timestamp;
				}
				else if (currentEvent is StopEvent)
				{
					double stop = (double)(currentEvent as StopEvent).stopTime / chart.resolution * 60.0 / laneBpm;
					double timestamp = currentEvent.timestamp;

					if (timestamp < currentTime)
						stop -= currentTime - timestamp;

					relPosition -= stop / currentScrollTime;
				}
				else
				{
					double timestamp = currentEvent.timestamp;
					double delta = timestamp - lastTimestamp;
					relPosition += delta / currentScrollTime;

					double oldRel = relPosition;
					if (relPosition >= 1.0)
						break;

					LaneObject laneObject = null;
					if (currentEvent is MeasureMarkerEvent && relPosition >= 0.0)
						laneObject = LaneObject.CreateMeasureMarker(currentEvent as MeasureMarkerEvent, relPosition);
					else if (currentEvent is NoteEvent)
					{
						NoteEvent noteEvent = currentEvent as NoteEvent;
						LongNoteEvent longNoteEvent = currentEvent as LongNoteEvent;
						LandmineEvent landmineEvent = currentEvent as LandmineEvent;
						LongNoteEndEvent lnEndEvent = currentEvent as LongNoteEndEvent;

						if (landmineEvent != null)
							laneObject = LaneObject.CreateNote(noteEvent.lane, noteEvent, relPosition);
						else if (lnEndEvent != null)
						{
							foreach (LaneObject lastObject in laneObjects)
							{
								LongNoteEvent note = lastObject.laneEvent as LongNoteEvent;
								if (note == null || note != lnEndEvent.startNote)
									continue;

								lastObject.positionEnd = relPosition;
								break;
							}
						}
						else if (longNoteEvent != null)
						{
							if (oldRel < 0.0)
								relPosition = oldRel;

							if (longNoteEvent.pulse <= pulse && index < activeLongNote)
								activeLongNote = index;

							// no idea when long note is going to end right now, so assume it doesn't
							laneObject = LaneObject.CreateLongNote(noteEvent.lane, longNoteEvent, relPosition, 1.0);
						}
						else if (relPosition >= 0.0)
							laneObject = LaneObject.CreateNote(noteEvent.lane, noteEvent, relPosition);
					}

					lastTimestamp = timestamp;

					if (laneObject != null)
						laneObjects.Add(laneObject);
				}
			}

			lastScrollEvent = Math.Min(activeLongNote, firstVisibleNote);
		}

		public void PrintJudge(JudgeText text, int number)
		{
			string countStr = number == 0 ? "" : number.ToString();

			judgeText = text;
			judgeNumber = number;

			if (text == JudgeText.Empty)
			{
				judgePrintTimer = 0.0;
				return;
			}

			judgePrintTimer = judgePrintTime;
			int totalWidth = 0;
			if (judgeFont == null)
			{
				if (text == JudgeText.PGreat)
				{
					//judgeAnimationIndex = 0;
					judgeTextRect = new Rectangle(0, 0 * 56, 153, 56);
					judgeNumberRect.y = 0 * 56;
				}
				else if (text == JudgeText.Great)
				{
					judgeTextRect = new Rectangle(0, 3 * 56, 153, 56);
					judgeNumberRect.y = 3 * 56;
				}
				else if (text == JudgeText.Good)
				{
					judgeTextRect = new Rectangle(0, 4 * 56, 123, 56);
					judgeNumberRect.y = 3 * 56;
				}
				else if (text == JudgeText.Bad)
					judgeTextRect = new Rectangle(124, 4 * 56, 95, 56);
				else if (text == JudgeText.Poor)
					judgeTextRect = new Rectangle(219, 4 * 56, 124, 56);

				// calculate offset for center point of judge text

				int timeWidth = judgeTextRect.width;
				int numWidth = judgeNumberRect.width;
				int numTotalWidth = countStr.Length * numWidth;
				totalWidth = timeWidth + numTotalWidth;
				judgePosOffset = -totalWidth / 2;
			}
			else
			{
				if (judgeText == JudgeText.PGreat)
					judgeStr = "GREAT";
				else
					judgeStr = judgeText.ToString().ToUpperInvariant();

				if (judgeText != JudgeText.Bad && judgeText != JudgeText.Poor)
					judgeStr += " " + judgeNumber.ToString();

				Int2 size = judgeFont.MeasureSize(judgeStr);
				judgePosOffset = -size.x / 2;
			}
		}

		private float GetLanePressFade(int lane)
		{
			double fadeTime = pressLaneFadeOutTime;
			double press = timer - laneLastPress[lane];
			if (laneActive[lane] > 0 && lane != 0)
				fadeTime = pressLaneFadeInTime;

			press = Math.Min(Math.Max(press, 0.0), fadeTime);

			if (laneActive[lane] == 0)
				press = fadeTime - press;

			if (fadeTime != 0.0f)
				return (float)(press / fadeTime);
			else
				return 1.0f;
		}

		private float GetKeyPressFade(int lane)
		{
			double fadeTime = pressKeyFadeOutTime;
			double press = timer - laneLastPress[lane];
			if (laneActive[lane] > 0 && lane != 0)
				fadeTime = pressKeyFadeInTime;

			press = Math.Min(Math.Max(press, 0.0), fadeTime);

			if (laneActive[lane] == 0)
				press = fadeTime - press;

			if (fadeTime != 0.0f)
				return (float)(press / fadeTime);
			else
				return 1.0f;
		}

		public void Render(double deltaTime)
		{
			Int2 laneStartPos = new Int2(28, 80);
			int noteHeight = textureNote1.height;		
			int laneHeight = textureLaneBG1.height;

			int laneTotalWidth = 0;
			for (int i = 0; i < keyCount; i++)
				laneTotalWidth += GetLaneTexture(i).width;

			UpdateLaneNotes();

			RenderBGALayers(deltaTime);

			SpriteRenderer spriteRenderer = renderer.spriteRenderer;
			spriteRenderer.Begin();
			spriteRenderer.Clear(Color.Black);

			RenderLanes(deltaTime, laneStartPos);

			RenderLaneGlow(deltaTime, new Rectangle(
				laneStartPos.x, laneStartPos.y + laneHeight - textureLaneGlow.height,
				laneTotalWidth, textureLaneGlow.height));

			RenderJudgeLine(deltaTime, new Rectangle(laneStartPos.x, laneStartPos.y + laneHeight,
				laneTotalWidth, noteHeight));

			RenderNotes(deltaTime, laneStartPos, laneTotalWidth);
			RenderProgressBar(deltaTime, laneStartPos + new Int2(-textureProgressBG.width, 0));
			RenderKeys(deltaTime, laneStartPos + new Int2(-27 - 11, laneHeight + noteHeight));
			RenderJudgeText(deltaTime, laneStartPos + new Int2(laneTotalWidth / 2, (int)(laneHeight * judgeTextY)));
			RenderBGA(deltaTime, new Rectangle(1280 - 720, 0, 720, 720));
			RenderNoteHit(deltaTime, laneStartPos + new Int2(0, laneHeight));
			RenderGauge(deltaTime, laneStartPos + new Int2(-9, laneHeight + 94));

			string songInfo = string.Format("{0} - {1}", chart.artist, chart.title);
			spriteRenderer.DrawText(Game.debugFont, songInfo, new Int2(0, Game.debugFont.pointSize * 1), Color.White);

			double scrollTime = SettingsManager.instance.gameplay.scrollTime * 1000;
			spriteRenderer.DrawText(Game.debugFont, "Scroll: " + scrollTime.ToString("0ms"), new Int2(10, 720 - Game.debugFont.pointSize - 10), Color.White);

			Int2 textPos = new Int2(390, 440);

			spriteRenderer.DrawText(Game.debugFont, "BPM: " + bpm.ToString(), textPos + new Int2(0, 720 - (Game.debugFont.pointSize * 2) - 450), Color.White);
			spriteRenderer.DrawText(Game.debugFont, "Measure: " + currentMeasure.ToString(), textPos + new Int2(0, 720 - (Game.debugFont.pointSize * 1) - 450), Color.White);

			// debug grading print
			string grade = "";
			int gradeInt = judge.GetGrade();
			if (gradeInt >= 5)
			{
				for (int i = 0; i <= gradeInt - 5; i++)
					grade += "A";
			}
			else if (gradeInt <= 4)
				grade += (char)('F' - gradeInt);

			textPos -= new Int2(30, 0);

			spriteRenderer.DrawText(Game.debugFont, "GR: " + grade + " (" + (judge.GetCurrentPercentage() * 100).ToString("0.0") + "%)",
				textPos + new Int2(0, -(Game.debugFont.pointSize * 4)), Color.White);
			spriteRenderer.DrawText(Game.debugFont, "EX: " + judge.GetScoreEx().ToString(),
				textPos + new Int2(0, -(Game.debugFont.pointSize * 3)), Color.White);
			spriteRenderer.DrawText(Game.debugFont, "Co: " + judge.scoreLargestCombo.ToString(),
				textPos + new Int2(0, -Game.debugFont.pointSize), Color.White);
			spriteRenderer.DrawText(Game.debugFont, "PG: " + judge.scorePGreatCount.ToString(),
				textPos, Color.LightGreen);
			spriteRenderer.DrawText(Game.debugFont, "Gr: " + judge.scoreGreatCount.ToString(),
				textPos + new Int2(0, Game.debugFont.pointSize), Color.Yellow);
			spriteRenderer.DrawText(Game.debugFont, "Go: " + judge.scoreGoodCount.ToString(),
				textPos + new Int2(0, Game.debugFont.pointSize * 2), Color.Orange);
			spriteRenderer.DrawText(Game.debugFont, "Ba: " + judge.scoreBadCount.ToString(),
				textPos + new Int2(0, Game.debugFont.pointSize * 3), Color.OrangeRed);
			spriteRenderer.DrawText(Game.debugFont, "Po: " + judge.scorePoorCount.ToString(),
				textPos + new Int2(0, Game.debugFont.pointSize * 4), Color.Red);

			spriteRenderer.DrawText(Game.debugFont, "F/S: ",
				textPos + new Int2(0, Game.debugFont.pointSize * 5), Color.Magenta);
			spriteRenderer.DrawText(Game.debugFont, judge.delayFastCount.ToString(),
				textPos + new Int2(90, Game.debugFont.pointSize * 5), Color.Blue);
			spriteRenderer.DrawText(Game.debugFont, judge.delaySlowCount.ToString(),
				textPos + new Int2(150, Game.debugFont.pointSize * 5), Color.Red);

			spriteRenderer.DrawText(Game.debugFont, "f/s: ",
				textPos + new Int2(0, Game.debugFont.pointSize * 6), Color.Magenta);
			spriteRenderer.DrawText(Game.debugFont, judge.delayFastCount2.ToString(),
				textPos + new Int2(90, Game.debugFont.pointSize * 6), Color.Blue);
			spriteRenderer.DrawText(Game.debugFont, judge.delaySlowCount2.ToString(),
				textPos + new Int2(150, Game.debugFont.pointSize * 6), Color.Red);

			spriteRenderer.DrawText(Game.debugFont, "delay: ",
				textPos + new Int2(0, Game.debugFont.pointSize * 7), Color.White);
			spriteRenderer.DrawText(Game.debugFont, (judge.GetAverageDelay() * 1000.0).ToString("0.00ms"),
				textPos + new Int2(90, Game.debugFont.pointSize * 7), Color.White);


			// log print
			if (DateTime.UtcNow.Subtract(Log.lastMessageTime).TotalSeconds <= 10.0)
			{
				string warningText = "";
				for (int i = Math.Max(Log.logList.Count - 30, 0); i < Log.logList.Count; i++)
				{
					if (DateTime.UtcNow.Subtract(Log.logList[i].timestamp).TotalSeconds <= 10.0)
					{
						if (Log.logList[i].repeated > 0)
							warningText += Log.logList[i].message + " [" + Log.logList[i].repeated.ToString() + "]\n";
						else
							warningText += Log.logList[i].message + "\n";
					}
				}

				if (warningText.Length > 0)
				{
					spriteRenderer.DrawText(Game.debugFont, warningText,
						new Int2(340, 0), Color.LightYellow);
				}
			}

			spriteRenderer.End();
		}

		private Texture2D GetLaneTexture(int lane)
		{
			if (keyCount == 6 || keyCount == 8)
			{
				if (lane == 0)
					return textureLaneBG3;
				else if (lane % 2 == 0)
					return textureLaneBG2;
			}
			else if (lane % 2 == 1)
				return textureLaneBG2;

			return textureLaneBG1;
		}

		private Texture2D GetLanePressTexture(int lane)
		{
			if (keyCount == 6 || keyCount == 8)
			{
				if (lane == 0)
					return textureLanePress3;
				else if (lane % 2 == 0)
					return textureLanePress2;
			}
			else if (lane % 2 == 1)
				return textureLanePress2;

			return textureLanePress1;
		}

		private void RenderLanes(double deltaTime, Int2 laneStartPos)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;

			int laneX = 0;
			for (int i = 0; i < keyCount; i++)
			{
				Texture2D laneBG = GetLaneTexture(i);
				Texture2D lanePress = GetLanePressTexture(i);

				// lane backgrounds	
				Int2 lanePos = laneStartPos + new Int2(laneX, 0);
				spriteRenderer.Draw(laneBG, lanePos, Color.White);
				laneX += laneBG.width;

				// lane press effects
				float fade = GetLanePressFade(i);
				Int2 lanePressPos = lanePos + new Int2(1, 0);
				spriteRenderer.Draw(lanePress, lanePressPos, Color.White * fade);
			}
		}

		private void RenderProgressBar(double deltaTime, Int2 progressBarPos)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;
			int laneHeight = textureLaneBG1.height;

			spriteRenderer.Draw(textureProgressBG, progressBarPos, Color.White);

			Int2 barPos = progressBarPos + new Int2(2, 2 + (int)Math.Round((laneHeight - textureProgressBar.height) * progress));
			spriteRenderer.Draw(textureProgressBar, barPos, Color.White);
		}

		private void RenderJudgeLine(double deltaTime, Rectangle lineRect)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;
			spriteRenderer.DrawColor(lineRect, new Color(0.75f, 0, 0));
		}

		private void RenderLaneGlow(double deltaTime, Rectangle glowPos)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;

			double glowAlpha = 1.0;
			if (chart != null && currentMeasure >= 0)
			{
				long positionAtMeasure = pulse - chart.measurePositions[currentMeasure].Item2;

				// snap to measure quarters
				glowAlpha = (double)positionAtMeasure / chart.resolution;
				glowAlpha -= (int)(glowAlpha);
				glowAlpha = 1.0 - glowAlpha;
			}

			spriteRenderer.Draw(textureLaneGlow, glowPos, Color.White * (float)glowAlpha);
		}

		float turntableRotation = 0.0f;
		private void RenderKeys(double deltaTime, Int2 keyStartPos)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;

			Int2 firstKeyPos = keyStartPos;
			int keyOffset = 0;
			int laneX = 0;

			// turntable for 5K/7K layout
			if (keyCount == 6 || keyCount == 8)
			{
				keyOffset = 1;

				// turntable background
				float fade = 1.0f - GetKeyPressFade(0);
				Int2 ttPos = firstKeyPos + new Int2(12 + 11, 8);
				spriteRenderer.Draw(textureTTBG, ttPos, new Color(1.0f, 1.0f * fade, 1.0f * fade));

				float rotationSpeed = (float)(deltaTime * Math.PI * 2 * 45.0 / 60.0);
				if (laneActive[0] != 0)
					turntableRotation -= rotationSpeed; // player touches the TT
				else
					turntableRotation += rotationSpeed;

				if (turntableRotation > Math.PI * 2)
					turntableRotation %= (float)Math.PI * 2;
				else if (turntableRotation < 0.0f)
					turntableRotation = (turntableRotation % (float)Math.PI * 2) + (float)Math.PI * 2;

				Int2 ttRotPos = ttPos + new Int2((textureTTBG.width) / 2, (textureTTBG.height) / 2);
				spriteRenderer.Draw(textureTTRotator, ttRotPos, turntableRotation, Color.White);

				firstKeyPos = ttPos;
				laneX = textureTTBG.width + 2;
			}
			else if (keyCount == 9)
			{
				firstKeyPos += new Int2(38, 8);
			}
			else
				throw new NotImplementedException("Skin layout for " + keyCount.ToString() + " lanes not defined");

			// keys
			for (int i = keyOffset; i < keyCount; i++)
			{
				Texture2D keyTexture = textureKeyW;
				if (i % 2 != keyOffset)
					keyTexture = textureKeyB;

				float fade = 1.0f - GetKeyPressFade(i);
				Int2 keyPos = firstKeyPos + new Int2(laneX, 0 - ((i % 2 != keyOffset) ? 5 : 0));
				spriteRenderer.Draw(keyTexture, keyPos, new Color(1.0f, 1.0f * fade, 1.0f * fade));

				laneX += keyTexture.width;
			}
		}

		private void RenderJudgeText(double deltaTime, Int2 judgePos)
		{
			if (judgeText == JudgeText.Empty)
				return;

			judgePos.x += (int)judgePosOffset;

			SpriteRenderer spriteRenderer = renderer.spriteRenderer;

			if (judgeFont != null)
			{
				float alpha = 1.0f;

				int colorIndex = 0;
				if (judgeText == JudgeText.PGreat)
					colorIndex = 3 + ((judgeAnimationIndex / 2) % 3);
				else
				{
					alpha = (judgeAnimationIndex / 1) % 2 == 0 ? 1.0f : 0.7f;
					if (judgeText == JudgeText.Great || judgeText == JudgeText.Good)
						colorIndex = 2;
					else if (judgeText == JudgeText.Bad)
						colorIndex = 1;
				}
				// quick and dirty outline effect

				Color color = judgeTextColors[colorIndex] * alpha;
				Color outlineColor = Color.Black * alpha;

				spriteRenderer.DrawTextOutline(judgeFont, judgeStr, judgePos, outlineColor, 3);
				spriteRenderer.DrawText(judgeFont, judgeStr, judgePos, color);

			}
			else // use font texture
			{
				throw new NotImplementedException("Judge font from texture not supported");
				/*int animationOffset = (judgeText == JudgeText.PGreat) ? ((judgeAnimationIndex % 3) * judgeTextRect.height) : 0;

				Rectangle textRect = judgeTextRect;
				textRect.y += animationOffset;

				// flicker effect
				if (judgeText == JudgeText.PGreat ||
					(judgeText != JudgeText.PGreat && judgeAnimationIndex % 2 == 0))
				{
					spriteRenderer.DrawPart(textureJudge, judgePos, textRect, Color.White);

					if (judgeNumber > 0)
					{
						string comboStr = judgeNumber.ToString();
						for (int i = 0; i < comboStr.Length; i++)
						{
							Rectangle numRect = judgeNumberRect;
							numRect.x += numRect.width * (comboStr[i] - '0');
							numRect.y += animationOffset;

							Int2 numPos = judgePos + new Int2(judgeTextRect.width + (judgeNumberRect.width * i), 0);
							spriteRenderer.DrawPart(textureJudge, numPos, numRect, Color.White);
						}
					}
				}*/
			}

			if (judgePrintTimer > 0.0)
				judgePrintTimer -= deltaTime;
			if (judgePrintTimer <= 0.0)
			{
				judgePrintTimer = 0.0;
				PrintJudge(JudgeText.Empty, 0);
			}

			judgeAnimationTimer += deltaTime;
			if (judgeAnimationTimer >= judgeAnimationFrametime)
			{
				judgeAnimationTimer %= judgeAnimationFrametime;
				judgeAnimationIndex = (judgeAnimationIndex + 1) % 6;
			}
		}

		private void RenderNotes(double deltaTime, Int2 laneStartPos, int laneTotalWidth)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;
			int noteHeight = textureNote1.height;
			int laneHeight = textureLaneBG1.height;

			// render measure markers first
			foreach (LaneObject laneObject in laneObjects)
			{
				if (!(laneObject.laneEvent is MeasureMarkerEvent))
					continue;

				int y = (int)((1.0 - laneObject.position) * laneHeight);
				int lineThickness = noteHeight / 2;
				float lineOffset = (noteHeight - lineThickness) / 2.0f;

				Rectangle measureLineRect = new Rectangle(laneStartPos.x,
					laneStartPos.y + (int)Math.Round(y + lineOffset),
					laneTotalWidth, lineThickness);
				spriteRenderer.DrawColor(measureLineRect, new Color(Color.White, 0.2f));
			}

			// render notes
			foreach (LaneObject laneObject in laneObjects)
			{
				NoteEvent note = laneObject.laneEvent as NoteEvent;
				if (note == null)
					continue;

				if (hideMissedNotes && laneObject.positionEnd < 0.0)
					continue;

				SubTexture texture = textureNote1;
				SubTexture textureActive = textureNote1LN;
				int lane = note.lane;
				int noteWidth = laneWidths[lane];

				// note color in different lanes
				if ((keyCount == 6 || keyCount == 8))
				{
					if (lane == 0)
					{
						texture = textureNote3;
						textureActive = textureNote3LN;
					}
					else if (lane % 2 == 0)
					{
						texture = textureNote2;
						textureActive = textureNote2LN;
					}
				}
				else if (lane % 2 == 1)
				{
					texture = textureNote2;
					textureActive = textureNote2LN;
				}

				int laneOffsetX = 1;
				for (int i = 0; i < lane; i++)
					laneOffsetX += laneWidths[i] + 2;

				int y = (int)Math.Round((1.0 - Math.Max(laneObject.position, 0.0)) * laneHeight);

				LongNoteEvent longNote = note as LongNoteEvent;
				if (longNote != null)
				{
					Color color = Color.White;

					int yEnd = (int)Math.Round((1.0 - laneObject.positionEnd) * laneHeight);
					int height = noteHeight + y - yEnd;

					// long note activation effect
					if (laneActive[lane] != 0 && laneObject.position <= 0)
						texture = textureActive;

					// dimm missed long notes
					if (judge.HasJudged(longNote.endNote))
						color *= 0.55f;

					Int2 notePos = laneStartPos + new Int2(laneOffsetX, yEnd);
					spriteRenderer.Draw(texture, new Rectangle(
						notePos.x, notePos.y, noteWidth, height), color);
				}
				else // regular note
				{
					Int2 notePos = laneStartPos + new Int2(laneOffsetX, y);
					spriteRenderer.Draw(texture, notePos, Color.White);
				}
			}
		}

		private void RenderBGALayers(double deltaTime)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;

			if (bgaLayerKeyed.texture == null)
				return;

			Texture2D layerTexture = bgaLayerKeyed.texture;
			Color color = bgaLayerKeyed.color;

			int bgaWidth = layerTexture.width;
			int bgaHeight = layerTexture.height;

			if (bgaRTTexture == null ||
				bgaWidth != bgaRTTexture.width ||
				bgaHeight != bgaRTTexture.height)
			{
				if (bgaFramebuffer != null)
					bgaFramebuffer.Dispose();

				if (bgaRTTexture != null)
					bgaRTTexture.Dispose();

				bgaRTTexture = new Texture2D(bgaWidth, bgaHeight, SharpBgfx.TextureFlags.RenderTarget);
				bgaFramebuffer = new FrameBuffer(bgaRTTexture);
			}

			if (bgaFramebuffer != null)
			{
				spriteRenderer.Begin(spriteRenderer.colorKeyProgram, SharpBgfx.TextureFlags.None);
				spriteRenderer.SetFrameBuffer(bgaFramebuffer);
				spriteRenderer.Clear(Color.Transparent);

				spriteRenderer.SetColorKey(Color.Black);

				spriteRenderer.Draw(layerTexture, new Int2(0, 0), color);

				spriteRenderer.End();
			}
		}

		private void RenderBGA(double deltaTime, Rectangle bgaRect)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;

			if (bgaBase.texture != null)
			{
				int baseWidth = bgaBase.texture.width;
				int baseHeight = bgaBase.texture.height;

				double scaleX = (double)bgaRect.width / baseWidth;
				double scaleY = (double)bgaRect.height / baseHeight;
				double scale = Math.Min(scaleX, scaleY);

				int offsetX = 0;
				int offsetY = 0;

				int newWidth = (int)Math.Round(baseWidth * scale);
				int newHeight = (int)Math.Round(baseHeight * scale);

				if (newWidth < bgaRect.width)
					offsetX = (int)Math.Round((bgaRect.width - newWidth) / 2.0);
				if (newHeight < bgaRect.height)
					offsetY = (int)Math.Round((bgaRect.height - newHeight) / 2.0);

				Rectangle rect = new Rectangle(bgaRect.x + offsetX, bgaRect.y + offsetY, newWidth, newHeight);
				spriteRenderer.Draw(bgaBase.texture, rect, bgaBase.color);

				Texture2D layerTexture = bgaLayer.texture ?? bgaRTTexture;
				if (layerTexture != null)
				{
					offsetX = 0;
					offsetY = 0;

					newWidth = (int)Math.Round(layerTexture.width * scale);
					newHeight = (int)Math.Round(layerTexture.height * scale);

					// BGA layers are centered along the X-axis only

					if (newWidth < bgaRect.width)
						offsetX = (int)Math.Round((bgaRect.width - newWidth) / 2.0);

					rect = new Rectangle(bgaRect.x + offsetX, bgaRect.y + offsetY, newWidth, newHeight);

					spriteRenderer.Draw(layerTexture, rect, Color.White);
				}
			}
		}

		private void RenderNoteHit(double deltaTime, Int2 hitStartPos)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;

			int xOffset = 0;
			for (int i = 0; i < keyCount; i++)
			{
				int laneWidth = laneWidths[i];
				if (noteHitFrames[i] >= 0)
				{
					Int2 effectSize = new Int2(100, 100);
					Rectangle effectRect = new Rectangle(hitStartPos + new Int2(xOffset + ((laneWidth - effectSize.x) / 2), -effectSize.y / 2),
						effectSize);
					spriteRenderer.Draw(textureNoteHitFrames[noteHitFrames[i]], effectRect, Color.White);

					noteHitFrameTimers[i] += deltaTime;
					while (noteHitFrameTimers[i] >= noteHitFrametime)
					{
						noteHitFrameTimers[i] -= noteHitFrametime;
						noteHitFrames[i]++;

						if (noteHitFrames[i] >= textureNoteHitFrames.Length)
						{
							noteHitFrames[i] = -1;
							noteHitFrameTimers[i] = 0.0;
							break;
						}
					}
				}
				xOffset += laneWidth + 2;
			}
		}

		private void RenderGauge(double deltaTime, Int2 gaugeStartPos)
		{
			SpriteRenderer spriteRenderer = renderer.spriteRenderer;

			gaugeTickTimer += deltaTime;
			if (gaugeTickTimer > gaugeTickFrametime)
			{
				gaugeTickTimer %= gaugeTickFrametime;
				gaugeTickState = gaugeTickState == 1 ? 0 : 1;
			}

			int tickWidth = textureGaugeTick.width;
			int tickHeight = textureGaugeTick.height;
			const double gaugeTarget = 0.8;
			const int topPos = (int)(50 * gaugeTarget) - 2;

			double gauge = judge.gaugeHealth * 100.0;
			int fullTicks = (int)(gauge * 0.5);
			double partialTickOpacity = (gauge * 0.5) - fullTicks;
			partialTickOpacity = Math.Pow(Math.Round(partialTickOpacity * 4) / 4.0, 0.25);

			Texture2D tickTexture = textureGaugeTick;
			Texture2D tickTextureOff = textureGaugeTickOff;
			for (int i = 0; i < 50; i++)
			{
				if (i > topPos)
				{
					tickTexture = textureGaugeTickTop;
					tickTextureOff = textureGaugeTickTopOff;
				}

				if (i < fullTicks)
					spriteRenderer.Draw(tickTexture, gaugeStartPos + new Int2(tickWidth * i, 0), Color.White);
				else
					spriteRenderer.Draw(tickTextureOff, gaugeStartPos + new Int2(tickWidth * i, 0), Color.White);

				if (i == fullTicks)
				{
					float opacity = (float)partialTickOpacity * gaugeTickState;
					spriteRenderer.Draw(tickTexture, gaugeStartPos + new Int2(tickWidth * i, 0), Color.White * opacity);
				}

			}

			spriteRenderer.DrawText(Game.debugFont, gauge.ToString("0") + "%",
				gaugeStartPos + new Int2((50 * tickWidth) + 5, (tickHeight - Game.debugFont.pointSize) / 2), Color.White);
		}
	}
}
