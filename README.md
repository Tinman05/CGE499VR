# Dino Feeding VR — เกมป้อนอาหารไดโนเสาร์บน Meta Quest 3

โครงงาน VR กลางภาค รายวิชา CGE499 (ภาคการศึกษา 1/2569)

> ผู้ใช้นั่งอยู่บนทางเดินรูปกากบาทเหนือหลุมไดโนเสาร์ หยิบเนื้อจากจาน โยนลงหลุม ไดโนเสาร์เดินหรือบินมากิน ต้องป้อนให้ทุกตัวไม่หิวจนหมดจนครบเวลา

## สมาชิกกลุ่ม

| รหัสนักศึกษา | ชื่อ–นามสกุล |
|---|---|
| TODO | TODO |
| TODO | TODO |

## วิธีเล่น

- เป้าหมาย: ดูแลไดโนเสาร์ทั้ง 4 ตัว (ที-เร็กซ์, ไทรเซอราทอปส์, แรปเตอร์, เทอราโนดอน) ไม่ให้ **ความหิวหมด** จนครบ 4 นาที — ชนะเมื่อรอดครบเวลา แพ้เมื่อมีตัวใดหิวจนล้ม
- หยิบเนื้อจากจานบนโต๊ะด้วยปุ่ม trigger แล้วโยนลงหลุม (รอบจะเริ่มเมื่อหยิบเนื้อชิ้นแรก)
- แถบสีเหนือหัวไดโนเสาร์บอกความหิว ตัวที่เดิน/วิ่ง/บินจะหิวเร็วกว่าตัวที่ยืนนิ่ง
- NPC ไกด์ (ชี้เลเซอร์แล้วกด trigger) อธิบายกติกาเป็นภาษาไทย
- เดินด้วยจอยสติ๊กซ้าย (ผู้เล่นนั่งเล่น)

### ปุ่มลัดสำหรับสาธิต (โหมดโชว์ / God mode)

เปิดอยู่ในซีน `DemoControls` (ปิดได้ที่ช่อง Enabled Shortcuts)

| ปุ่ม | ผล |
|---|---|
| จอยซ้าย X | ป้อนไดโนเสาร์ทุกตัวจนอิ่ม |
| จอยซ้าย Y | ทำให้ไดโนเสาร์หนึ่งตัวหิวจนล้ม (แสดงท่าล้มและป้ายแพ้) |
| จอยขวา A | ชนะรอบนี้ทันที |
| จอยขวา B | เริ่มรอบใหม่ |

## เปิดโปรเจกต์

- Unity **6000.3.23f1** (Universal Render Pipeline), Meta XR SDK (Interaction SDK / Building Blocks) v207, OpenXR
- เปิดโฟลเดอร์นี้ด้วย Unity Hub แล้วเปิดซีน `Assets/Scenes/SampleScene.unity`
- Build สำหรับ Quest: ตั้งแพลตฟอร์มเป็น Android (IL2CPP, ARM64, Vulkan) แล้ว Build ซีนเดียวนี้ — ผลที่ได้ลงด้วย Meta Quest Developer Hub หรือ `adb install`
- สถานะงานและรายการที่ทำ/ค้าง: ดู [PROGRESS.md](PROGRESS.md)

## ผลงานที่ทีมพัฒนาเอง และส่วนที่นำมาจากแหล่งอื่น

> **หมายเหตุเรื่องการใช้ AI:** โค้ดสคริปต์ทั้งหมดใน `Assets/Scripts` ถูกเขียนโดยร่วมกับผู้ช่วย AI (Claude Code ของ Anthropic) โดยทีมเป็นผู้กำหนดแนวคิด ทิศทาง ทดสอบในแว่นจริง และสั่งแก้ซ้ำหลายรอบ

### พัฒนาขึ้นเอง (โดยทีม)

| ส่วน | รายละเอียด / ไฟล์ |
|---|---|
| แนวคิดเกมและกติกา | เกมป้อนอาหาร, ระบบความหิว, ชนะ/แพ้, ความยากที่เพิ่มขึ้นตามเวลา |
| การจัดฉาก | วางกรง/ทางเดิน/ไดโนเสาร์/โต๊ะ/NPC, ตัวกั้นทางเดินที่มองไม่เห็น, NavMesh แยกคอก, วางรั้ว 23 ชิ้น, ปลูกต้นไม้ 72 ต้น, ท้องฟ้า/หมอก/พื้นหญ้าไกล |
| AI ไดโนเสาร์ | `Dinosaur.cs` (เดินสุ่ม, เดินหาเนื้อ, หันหน้าแล้วกิน), `FlyingDinosaur.cs` (บิน ขึ้น-ลงตรงๆ ไม่ชนกำแพง), `DinoHunger.cs`, `DinoDeath.cs` (ragdoll ตอนหิวจนล้ม), `DinoVoice.cs` |
| เกมเพลย์/ระบบ | `HungerGame.cs`, `Meat.cs`, `MeatSpawner.cs`, `MeatThrowBoost.cs`, `FeedingFeedback.cs`, `GameBanner.cs`, `GameAudio.cs`, `Haptics.cs`, `NpcDialogue.cs`, `SeatedHeightOffset.cs`, `PlayerOnlyBarrier.cs`, `DemoControls.cs`, `PerfLogger.cs` |
| NPC | ลดจำนวนสามเหลี่ยม 659,000 → 94,000, สร้าง UV, **ลงสีเท็กซ์เจอร์ด้วยมือใน Blender** (`Assets/Textures/NPC_Paint.png`) |
| ข้อความไทย | ฟอนต์ TextMeshPro จาก Noto Sans Thai ที่สร้างเองให้วรรณยุกต์ไม่ซ้อนกัน (`Assets/Fonts`) |

### นำมาจากแหล่งอื่น

| ส่วน | แหล่งที่มา / ลิขสิทธิ์ |
|---|---|
| Meta XR SDK (Interaction SDK, Building Blocks, OVR) | Meta Platforms — ติดตั้งผ่าน Package Manager |
| Unity packages (URP, AI Navigation, TextMeshPro, Input System, XR) | Unity Technologies |
| โมเดลไดโนเสาร์ 4 ตัว (`Assets/Models/drive-download-.../*.fbx`) | TODO: ระบุแหล่งที่มา / ผู้สร้าง |
| โมเดลกรง/ทางเดิน/รั้ว (`Assets/Models/Cago`) | TODO: ระบุแหล่งที่มา / ผู้สร้าง |
| โมเดลภูมิประเทศและต้นไม้ (`Assets/TerrainGameVR.fbx`) | TODO: ระบุแหล่งที่มา / ผู้สร้าง |
| โมเดล NPC ต้นฉบับ (`Assets/Models/NPC.obj`) | TODO: ระบุแหล่งที่มา / ผู้สร้าง |
| โมเดลเนื้อและจาน (`Assets/Models/Steak`) | มาจากโมเดล "Team Fortress 2 — Buffalo Steak Sandvich" (Valve) — TODO: ใส่ลิงก์ต้นทางและสัญญาอนุญาต |
| ฟอนต์ Noto Sans Thai | Google Fonts, SIL Open Font License 1.1 (`Assets/Fonts/NotoSansThai-OFL.txt`) |
| ท้องฟ้า "Kloofendal 48d Partly Cloudy (Pure Sky)" | Greg Zaal และ Jarod Guest, Poly Haven, CC0 — <https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky> |
| เสียงเอฟเฟกต์ | CC0 จาก OpenGameArt (80 CC0 creature SFX โดย rubberduck, T-rex calls) และ Kenney (RPG audio, Interface Sounds) — รายละเอียดใน `Assets/Audio/CREDITS.txt` |
