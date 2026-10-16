-- ============================================================
-- v3 - Tekla HW150*150*7*10 beams 2 to 5 (beam 1 already exists)
-- Every command below was proven to work when typed interactively
-- on beam 1, except OWNER (used to step back up to the framework).
--
-- BEFORE RUNNING: click FRMW TEKLA-TEST in the Model Explorer
-- so it is the current element.
--
-- Placeholders still to confirm: SPRE /EU-HE160A, MATR /GR275
-- ============================================================

-- Tekla id 207654937
NEW SCTN
SPRE /EU-HE160A
MATR /GR275
JUSL NA
POSS E -9319.7mm N 8375.0mm U 7591.4mm
POSE E -9319.7mm N 7825.0mm U 7591.4mm
:TEKLA_GUID 'e63bc546-35ab-4ce4-b71f-76c21e8b964e'
:TEKLA_PROFILE 'HW150*150*7*10'
:TEKLA_MATERIAL 'Q355B'
OWNER

-- Tekla id 207656935
NEW SCTN
SPRE /EU-HE160A
MATR /GR275
JUSL NA
POSS E -7097.5mm N 8375.0mm U 7560.5mm
POSE E -7097.5mm N 7825.0mm U 7560.5mm
:TEKLA_GUID '11c7307e-2cd0-4a59-9ba6-ecf8b4eb9fef'
:TEKLA_PROFILE 'HW150*150*7*10'
:TEKLA_MATERIAL 'Q355B'
OWNER

-- Tekla id 207078672
NEW SCTN
SPRE /EU-HE160A
MATR /GR275
JUSL NA
POSS E -1410.0mm N 225.0mm U 4320.9mm
POSE E -210.0mm N 225.0mm U 4320.9mm
:TEKLA_GUID '02e5b2a6-7ab5-4bdd-ba57-0187696eb9c3'
:TEKLA_PROFILE 'HW150*150*7*10'
:TEKLA_MATERIAL 'Q355B'
OWNER

-- Tekla id 207078767
NEW SCTN
SPRE /EU-HE160A
MATR /GR275
JUSL NA
POSS E -210.0mm N 225.0mm U 3320.9mm
POSE E -1060.0mm N 225.0mm U 4170.9mm
:TEKLA_GUID '90650d8e-2e7e-4833-92f3-062f7ea6c0f4'
:TEKLA_PROFILE 'HW150*150*7*10'
:TEKLA_MATERIAL 'Q355B'
OWNER

SAVEWORK
