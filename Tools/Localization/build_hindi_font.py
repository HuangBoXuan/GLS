"""Bake catalog Hindi words with HarfBuzz for Unity's legacy UI Text renderer.

Install requirements in Library/LanguageTools or pass --tools. The runtime keeps
Unicode source text; only the display uses positioned word glyphs in a derived
OFL font. Rebuild whenever Hindi catalog text changes.
"""
from pathlib import Path
import argparse, hashlib, io, json, re, sys

root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--tools',type=Path,default=root/'Library/LanguageTools')
args=parser.parse_args();sys.path.insert(0,str(args.tools))
import uharfbuzz as hb
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.recordingPen import DecomposingRecordingPen
from fontTools.pens.cu2quPen import Cu2QuPen

resources=root/'Assets/LoopSort/Resources/LoopSort'
catalog=json.loads((resources/'Localization/catalog.json').read_text(encoding='utf-8'))
values=[entry['hi'] for entry in catalog['entries']]+['हिन्दी']
words=sorted({word for text in values for word in re.findall(r'[\u0900-\u097f\u200c\u200d]+',text)})
assert len(words)<6400
source=root/'ArtSource/Fonts/Hindi/Baloo2-Variable.ttf'
font=instantiateVariableFont(TTFont(source),{'wght':600},inplace=False)
buffer=io.BytesIO();font.save(buffer)
hbf=hb.Font(hb.Face(buffer.getvalue()));upem=font['head'].unitsPerEm;hbf.scale=(upem,upem)
glyphset=font.getGlyphSet();order=list(font.getGlyphOrder());mapping=[]
for index,word in enumerate(words):
    text=hb.Buffer();text.add_str(word);text.guess_segment_properties();text.language='hi';hb.shape(hbf,text)
    pen=TTGlyphPen(None);x=y=0
    for info,position in zip(text.glyph_infos,text.glyph_positions):
        assert info.codepoint!=0,word
        outline=DecomposingRecordingPen(glyphset);glyphset[order[info.codepoint]].draw(outline)
        outline.replay(TransformPen(pen,(1,0,0,1,x+position.x_offset,y+position.y_offset)))
        x+=position.x_advance;y+=position.y_advance
    glyph=pen.glyph();glyph.recalcBounds(font['glyf']);name='loopSortWord'+str(index)
    font['glyf'][name]=glyph;font['hmtx'].metrics[name]=(x,getattr(glyph,'xMin',0));order.append(name)
    for table in font['cmap'].tables:
        if table.isUnicode():table.cmap[0xe000+index]=name
    mapping.append({'word':word,'glyph':chr(0xe000+index)})
# Include punctuation/check marks that are not in the source Hindi font.
characters={c for value in values if re.search(r'[\u0900-\u097f]',value) for c in value if ord(c)>=32 and not ('\u0900'<=c<='\u097f') and c not in '\u200c\u200d'}
fallback=TTFont(resources/'Fonts/NotoSansSC-Bold.otf');fallbackGlyphs=fallback.getGlyphSet();fallbackMap=fallback.getBestCmap();scale=upem/fallback['head'].unitsPerEm
for character in sorted(characters):
    code=ord(character)
    if code in font.getBestCmap():continue
    assert code in fallbackMap,repr(character)
    name='loopSortSymbol'+str(code);pen=TTGlyphPen(None)
    fallbackGlyphs[fallbackMap[code]].draw(Cu2QuPen(TransformPen(pen,(scale,0,0,scale,0,0)),max_err=1))
    glyph=pen.glyph();glyph.recalcBounds(font['glyf']);font['glyf'][name]=glyph
    width=fallback['hmtx'].metrics[fallbackMap[code]][0];font['hmtx'].metrics[name]=(round(width*scale),getattr(glyph,'xMin',0));order.append(name)
    for table in font['cmap'].tables:
        if table.isUnicode():table.cmap[code]=name
font.setGlyphOrder(order);font['maxp'].numGlyphs=len(order)
for nameid,value in [(1,'LoopSort Hindi'),(3,'LoopSortHindi-1'),(4,'LoopSort Hindi SemiBold'),(6,'LoopSortHindi-SemiBold'),(16,'LoopSort Hindi')]:
    font['name'].setName(value,nameid,3,1,0x409)
font.recalcTimestamp=False
font.save(resources/'Fonts/LoopSortHindi.ttf')
(resources/'Fonts/OFL-Baloo2.txt').write_bytes((root/'ArtSource/Fonts/Hindi/Baloo2-OFL.txt').read_bytes())
output=resources/'Localization/hindi-shaping.json'
output.write_text(json.dumps({'words':mapping},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
receipt={'words':len(words),'fonttools':'4.68.0','uharfbuzz':'0.57.0','sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest(),'mappingSha256':hashlib.sha256(output.read_bytes()).hexdigest()}
(resources/'Localization/hindi-font-manifest.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
print('Built Hindi font with',len(words),'correctly shaped catalog words.')
