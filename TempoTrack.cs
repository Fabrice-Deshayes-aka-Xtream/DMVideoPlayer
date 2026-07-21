using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DMVideoPlayer
{
    public class TempoEvent
    {
        public double Bpm { get; set; }
        public double Ppq { get; set; }
        public double TimeInSeconds { get; set; }
    }

    public class TempoTrack
    {
        private List<TempoEvent> _tempoEvents = new List<TempoEvent>();
        private List<double> _beatTimes = new List<double>();
        private const double PPQ_PER_QUARTER = 480.0;

        public bool IsLoaded => _tempoEvents.Count > 0;

        public double GetBpmAtTime(double seconds)
        {
            if (_tempoEvents.Count == 0)
                return 120.0; // Default BPM

            if (seconds <= 0)
                return _tempoEvents[0].Bpm;

            for (int i = _tempoEvents.Count - 1; i >= 0; i--)
            {
                if (_tempoEvents[i].TimeInSeconds <= seconds)
                {
                    return _tempoEvents[i].Bpm;
                }
            }

            return _tempoEvents[0].Bpm;
        }

        /// <summary>
        /// Finds the exact time of the nearest beat (past or future) relative to the given time
        /// </summary>
        public double GetNearestBeatTime(double currentTimeInSeconds)
        {
            if (_beatTimes.Count == 0)
                return -1.0;

            // Binary search for the nearest beat
            int index = _beatTimes.BinarySearch(currentTimeInSeconds);

            if (index >= 0)
            {
                // Exact time of a found beat
                return _beatTimes[index];
            }
            else
            {
                // BinarySearch returns ~index of the next larger element
                int nextIndex = ~index;

                if (nextIndex == 0)
                {
                    // Before the first beat
                    return _beatTimes[0];
                }
                else if (nextIndex >= _beatTimes.Count)
                {
                    // After the last beat
                    return _beatTimes[_beatTimes.Count - 1];
                }
                else
                {
                    // Between two beats - pick the closest one
                    double prevBeat = _beatTimes[nextIndex - 1];
                    double nextBeat = _beatTimes[nextIndex];

                    double distToPrev = currentTimeInSeconds - prevBeat;
                    double distToNext = nextBeat - currentTimeInSeconds;

                    return (distToPrev <= distToNext) ? prevBeat : nextBeat;
                }
            }
        }

        /// <summary>
        /// Finds the exact time of the next upcoming beat
        /// </summary>
        public double GetNextBeatTime(double currentTimeInSeconds)
        {
            if (_beatTimes.Count == 0)
                return -1.0;

            for (int i = 0; i < _beatTimes.Count; i++)
            {
                if (_beatTimes[i] > currentTimeInSeconds)
                {
                    return _beatTimes[i];
                }
            }

            return -1.0; // No more beats after this time
        }

        public static TempoTrack? LoadFromFile(string smtFilePath)
        {
            if (!File.Exists(smtFilePath))
            {
                return null;
            }

            try
            {
                var tempoTrack = new TempoTrack();
                tempoTrack.ParseSmtFile(smtFilePath);
                return tempoTrack.IsLoaded ? tempoTrack : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading tempo track: {ex.Message}");
                return null;
            }
        }

        private void ParseSmtFile(string filePath)
        {
            var doc = XDocument.Load(filePath);

            var tempoEvents = doc.Descendants("obj")
                .Where(obj => obj.Attribute("class")?.Value == "MTempoEvent")
                .Select(obj =>
                {
                    var bpmElement = obj.Descendants("float")
                        .FirstOrDefault(f => f.Attribute("name")?.Value == "BPM");
                    var ppqElement = obj.Descendants("float")
                        .FirstOrDefault(f => f.Attribute("name")?.Value == "PPQ");

                    if (bpmElement != null && ppqElement != null)
                    {
                        return new TempoEvent
                        {
                            Bpm = double.Parse(bpmElement.Attribute("value")?.Value ?? "120",
                                System.Globalization.CultureInfo.InvariantCulture),
                            Ppq = double.Parse(ppqElement.Attribute("value")?.Value ?? "0",
                                System.Globalization.CultureInfo.InvariantCulture),
                            TimeInSeconds = 0
                        };
                    }
                    return null;
                })
                .Where(e => e != null)
                .Cast<TempoEvent>()
                .OrderBy(e => e.Ppq)
                .ToList();

            if (tempoEvents.Count > 0)
            {
                _tempoEvents = tempoEvents;
                ConvertPpqToSeconds();
                PrecomputeBeatTimes();
            }
        }

        private void ConvertPpqToSeconds()
        {
            if (_tempoEvents.Count == 0)
                return;

            double currentTimeInSeconds = 0.0;
            _tempoEvents[0].TimeInSeconds = 0.0;

            for (int i = 1; i < _tempoEvents.Count; i++)
            {
                var previousEvent = _tempoEvents[i - 1];
                var currentEvent = _tempoEvents[i];

                double ppqDelta = currentEvent.Ppq - previousEvent.Ppq;
                double quarterNotes = ppqDelta / PPQ_PER_QUARTER;
                double secondsPerQuarter = 60.0 / previousEvent.Bpm;
                double timeDelta = quarterNotes * secondsPerQuarter;

                currentTimeInSeconds += timeDelta;
                currentEvent.TimeInSeconds = currentTimeInSeconds;
            }
        }

        private void PrecomputeBeatTimes()
        {
            _beatTimes.Clear();

            if (_tempoEvents.Count == 0)
                return;

            // Start at the first tempo event
            var firstEvent = _tempoEvents[0];
            double currentBeatTime = firstEvent.TimeInSeconds;
            _beatTimes.Add(currentBeatTime);

            // Generate beats continuously over 5 minutes (or until the end)
            double maxTime = firstEvent.TimeInSeconds + 300.0; // 5 minutes max
            int currentEventIndex = 0;

            while (currentBeatTime < maxTime && currentEventIndex < _tempoEvents.Count)
            {
                var currentEvent = _tempoEvents[currentEventIndex];
                double secondsPerBeat = 60.0 / currentEvent.Bpm;

                // Compute the next beat
                currentBeatTime += secondsPerBeat;

                // If we've passed the next tempo event, move to the next one
                while (currentEventIndex + 1 < _tempoEvents.Count &&
                       currentBeatTime >= _tempoEvents[currentEventIndex + 1].TimeInSeconds)
                {
                    currentEventIndex++;
                    // Recalculating secondsPerBeat with the new BPM will happen on the next iteration
                }

                // Add the beat if we haven't exceeded the time limit
                if (currentBeatTime < maxTime)
                {
                    _beatTimes.Add(currentBeatTime);
                }
            }
        }
    }
}
