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
        /// Trouve le temps exact du beat le plus proche (passé ou futur) par rapport au temps donné
        /// </summary>
        public double GetNearestBeatTime(double currentTimeInSeconds)
        {
            if (_beatTimes.Count == 0)
                return -1.0;

            // Recherche binaire du beat le plus proche
            int index = _beatTimes.BinarySearch(currentTimeInSeconds);

            if (index >= 0)
            {
                // Temps exact d'un beat trouvé
                return _beatTimes[index];
            }
            else
            {
                // BinarySearch retourne ~index du prochain élément plus grand
                int nextIndex = ~index;

                if (nextIndex == 0)
                {
                    // Avant le premier beat
                    return _beatTimes[0];
                }
                else if (nextIndex >= _beatTimes.Count)
                {
                    // Après le dernier beat
                    return _beatTimes[_beatTimes.Count - 1];
                }
                else
                {
                    // Entre deux beats - choisir le plus proche
                    double prevBeat = _beatTimes[nextIndex - 1];
                    double nextBeat = _beatTimes[nextIndex];

                    double distToPrev = currentTimeInSeconds - prevBeat;
                    double distToNext = nextBeat - currentTimeInSeconds;

                    return (distToPrev <= distToNext) ? prevBeat : nextBeat;
                }
            }
        }

        /// <summary>
        /// Trouve le temps exact du prochain beat à venir
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

            return -1.0; // Plus de beats après ce temps
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

            // Commencer au premier événement de tempo
            var firstEvent = _tempoEvents[0];
            double currentBeatTime = firstEvent.TimeInSeconds;
            _beatTimes.Add(currentBeatTime);

            // Générer les beats de manière continue sur 5 minutes (ou jusqu'à la fin)
            double maxTime = firstEvent.TimeInSeconds + 300.0; // 5 minutes max
            int currentEventIndex = 0;

            while (currentBeatTime < maxTime && currentEventIndex < _tempoEvents.Count)
            {
                var currentEvent = _tempoEvents[currentEventIndex];
                double secondsPerBeat = 60.0 / currentEvent.Bpm;

                // Calculer le prochain beat
                currentBeatTime += secondsPerBeat;

                // Si on a dépassé le prochain événement de tempo, passer au suivant
                while (currentEventIndex + 1 < _tempoEvents.Count &&
                       currentBeatTime >= _tempoEvents[currentEventIndex + 1].TimeInSeconds)
                {
                    currentEventIndex++;
                    // Recalculer secondsPerBeat avec le nouveau BPM sera fait à la prochaine itération
                }

                // Ajouter le beat si on n'a pas dépassé la limite de temps
                if (currentBeatTime < maxTime)
                {
                    _beatTimes.Add(currentBeatTime);
                }
            }
        }
    }
}
