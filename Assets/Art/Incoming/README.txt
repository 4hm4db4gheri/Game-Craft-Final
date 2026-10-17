Drop AI-generated art here, named as in docs/ART_MASTER_BRIEF.md (e.g. ENM_A1_RunnerWolf_move_f6.png).
The importer sets scale (128 px per unit), pivot, frame slicing and removes a flat magenta background automatically.
Then run Unity menu: Hollow > Art > Apply Incoming Art. Rebuilding the scene keeps the art.

Generated/ holds animation sheets made from the single drawn frames by Tools/ArtGen/animate.py. A hand-drawn
sheet of the same state (e.g. ENM_A1_RunnerWolf_move_f6.png here) always wins over a generated one.
