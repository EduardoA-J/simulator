import importlib.util, collections
from pathlib import Path
P=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('generator',P/'Tools/generate_avion_assets.py')
g=importlib.util.module_from_spec(spec);spec.loader.exec_module(g)
def normal(mesh,face):
 a,b,c=[mesh.verts[i-1] for i,_ in face]
 u=[b[i]-a[i] for i in range(3)];v=[c[i]-a[i] for i in range(3)]
 return (u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0])
checks=[]
for shape in ['box','cylinder','cone']:
 m=g.Mesh(shape)
 if shape=='box':m.box(0,0,0,1,1,1)
 else:getattr(m,shape)(0,0,0,1,1)
 m.validate()
 for face in m.faces:
  center=[sum(m.verts[i-1][axis] for i,_ in face)/3 for axis in range(3)]
  assert sum(a*b for a,b in zip(normal(m,face),center))>0,shape
 checks.append(shape+' outward normals')
for style in range(12):
 m=g.paper_plane('test',style);m.validate();edges=collections.Counter()
 for j,face in enumerate(m.faces[:16]):
  assert normal(m,face)[1]*(1 if j%2==0 else -1)>0,(style,j)
  ids=[i for i,_ in face]
  for a,b in zip(ids,ids[1:]+ids[:1]):edges[(a,b)]+=1
 assert all(edges[(b,a)]==n for (a,b),n in edges.items()),style
 checks.append('plane '+str(style)+' closed shell and consistent normals')
print('\n'.join('PASS '+c for c in checks))
print(len(checks),'geometry checks passed')
