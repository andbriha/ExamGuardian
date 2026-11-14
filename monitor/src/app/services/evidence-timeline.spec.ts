import { findEvidenceChunk } from './evidence-timeline';
import { VideoChunkDto } from './violation.service';

describe('Evidence timeline', () => {
  const chunks: VideoChunkDto[] = [
    {chunkIndex: 1, fileName: '1.mp4', sizeBytes: 1, url: '/1', metadata: {startOffsetSeconds: 4, durationSeconds: 58, stream: 'camera'}},
    {chunkIndex: 3, fileName: '3.mp4', sizeBytes: 1, url: '/3', metadata: {startOffsetSeconds: 124, durationSeconds: 19, stream: 'camera'}}
  ];
  it('uses capture ranges rather than presumed minute boundaries', () => {
    expect(findEvidenceChunk(chunks,61)?.chunkIndex).toBe(1);
    expect(findEvidenceChunk(chunks,130)?.chunkIndex).toBe(3);
  });
  it('does not substitute another chunk for missing evidence', () => {
    expect(findEvidenceChunk(chunks,90)).toBeUndefined();
    expect(findEvidenceChunk(chunks,143)).toBeUndefined();
  });
  it('keeps legacy evidence with unknown timing outside automatic seeks', () => {
    expect(findEvidenceChunk([{chunkIndex: 1,fileName:'old.mp4',sizeBytes:1,url:'/old'}],4)).toBeUndefined();
  });
});
