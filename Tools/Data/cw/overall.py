import csv,sys
rows=list(csv.reader(open(sys.argv[1])))
names=rows[0][1:]
out=[]
for r in rows[1:]:
    vals=[float(v) for v in r[1:] if v!='']
    out.append((sum(vals)/len(vals), r[0]))
for avg,n in sorted(out,reverse=True): print(f"{avg:5.0%}  {n}")
