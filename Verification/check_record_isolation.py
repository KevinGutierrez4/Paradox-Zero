from pathlib import Path
p=Path('Assets/OneMoreTime/Editor/DemoPlayValidation.cs');s=p.read_text(encoding='utf-8-sig').replace('''            Set(game, "persistRecords", false);''','''            Set(game, "persistRecords", false);
            int storedRecord = PlayerPrefs.GetInt("OMT.BestScore.v1", 0);''').replace('''            Check(errors == 0, "No runtime exceptions during acceptance");''','''            Check(PlayerPrefs.GetInt("OMT.BestScore.v1", 0) == storedRecord, "Acceptance leaves persisted record unchanged");
            Check(errors == 0, "No runtime exceptions during acceptance");''');p.write_text(s,encoding='utf-8')
